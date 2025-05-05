using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.EventPublish;
using Aevatar.Core.Abstractions.Projections;
using Aevatar.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Serialization;
using Orleans.Streams;

namespace Aevatar.Core;

[GAgent]
[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public abstract class
    GAgentBase<TState, TStateLogEvent>
    : GAgentBase<TState, TStateLogEvent, EventBase, ConfigurationBase>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>;

[GAgent]
[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public abstract class
    GAgentBase<TState, TStateLogEvent, TEvent>
    : GAgentBase<TState, TStateLogEvent, TEvent, ConfigurationBase>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase;

[GAgent]
[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public abstract partial class
    GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
    : JournaledGrain<TState, StateLogEventBase<TStateLogEvent>>, IStateGAgent<TState>, IExtGAgent
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
    where TConfiguration : ConfigurationBase
{
    private Lazy<IStreamProvider> LazyStreamProvider => new(()
        => this.GetStreamProvider(AevatarCoreConstants.StreamProvider));

    private Lazy<IGAgentFactory> LazyGAgentFactory => new(()
        => ServiceProvider.GetRequiredService<IGAgentFactory>());

    protected IStreamProvider StreamProvider => LazyStreamProvider.Value;
    protected IGAgentFactory GAgentFactory => LazyGAgentFactory.Value;

    public ILogger Logger { get; set; } = NullLogger.Instance;

    private readonly List<EventWrapperBaseAsyncObserver> _observers = [];

    private IStateDispatcher? StateDispatcher { get; set; }
    protected AevatarOptions? AevatarOptions { get; private set; }

    private IStreamCoordinatorGrain? _coordinator;

    public async Task ActivateAsync()
    {
        await Task.Yield();
    }

    public async Task RegisterAsync(IGAgent gAgent)
    {
        var grainId = gAgent.GetGrainId();
        if (grainId == this.GetGrainId())
        {
            Logger.LogError($"Cannot register GAgent with same GrainId.");
            return;
        }

        Logger.LogDebug("GrainId [{GrainId}] register child {Parent}", GrainId.ToString(), grainId.ToString());

        var childStreamCoordinator = GrainFactory.GetGrain<IStreamCoordinatorGrain>(grainId.ToString());
        if (await childStreamCoordinator.SetParentAsync(GrainId))
        {
            var groupIndex = await _coordinator!.RegisterChildAsync(grainId);
            await childStreamCoordinator.SetGroupIndexAsync(groupIndex);
            await OnRegisterAgentAsync(grainId);
        }
    }

    public async Task RegisterManyAsync(List<IGAgent> gAgents)
    {
        if (gAgents.IsNullOrEmpty())
        {
            return;
        }

        gAgents.RemoveAll(g => g.GetGrainId() == this.GetGrainId());
        if (gAgents.IsNullOrEmpty())
        {
            return;
        }

        var grainIds = gAgents.Select(g => g.GetGrainId()).ToList();
        var successGrainIds = new List<GrainId>();
        var streamCoordinators = new List<IStreamCoordinatorGrain>();
        foreach (var gAgent in gAgents)
        {
            var childStreamCoordinator = GrainFactory.GetGrain<IStreamCoordinatorGrain>(gAgent.GetGrainId().ToString());
            streamCoordinators.Add(childStreamCoordinator);
            if (await childStreamCoordinator.SetParentAsync(GrainId))
            {
                successGrainIds.Add(gAgent.GetGrainId());
            }
        }

        // TODO: Optimize.
        var groupIndex = await _coordinator!.RegisterManyChildAsync(successGrainIds);
        foreach (var coordinator in streamCoordinators)
        {
            await coordinator.SetGroupIndexAsync(groupIndex);
        }

        await OnRegisterAgentManyAsync(grainIds);
    }

    public async Task SubscribeToAsync(IGAgent gAgent)
    {
        var grainId = gAgent.GetGrainId();
        Logger.LogDebug("GrainId [{GrainId}] subscribe to {Parent}", GrainId.ToString(), grainId.ToString());
        await _coordinator!.SetParentAsync(grainId);
    }

    public async Task UnsubscribeFromAsync(IGAgent gAgent)
    {
        var grainId = gAgent.GetGrainId();
        Logger.LogDebug("GrainId [{GrainId}] unsubscribe from {Parent}", GrainId.ToString(), grainId.ToString());
        await _coordinator!.ClearParentAsync(grainId);
    }

    public async Task UnregisterAsync(IGAgent gAgent)
    {
        var grainId = gAgent.GetGrainId();
        Logger.LogDebug("GrainId [{GrainId}] unregister child {Child}", GrainId.ToString(), grainId.ToString());
        await _coordinator!.UnregisterChildAsync(gAgent.GetGrainId());
        await OnUnregisterAgentAsync(gAgent.GetGrainId());
    }

    public async virtual Task<List<Type>?> GetAllSubscribedEventsAsync(bool includeBaseHandlers = false)
    {
        var eventHandlerMethods = GetEventHandlerMethods(GetType());
        eventHandlerMethods = eventHandlerMethods.Where(m =>
            m.Name != nameof(ForwardEventAsync) && m.Name != nameof(PerformConfigAsync));
        var handlingTypes = eventHandlerMethods
            .Select(m => m.GetParameters().First().ParameterType);
        if (!includeBaseHandlers)
        {
            handlingTypes = handlingTypes.Where(t => t != typeof(RequestAllSubscriptionsEvent));
        }

        return handlingTypes.ToList();
    }

    public async Task<List<GrainId>> GetChildrenAsync()
    {
        return await _coordinator!.GetChildrenAsync();
    }

    public async Task<GrainId> GetParentAsync()
    {
        return await _coordinator!.GetParentAsync();
    }

    public virtual Task<Type?> GetConfigurationTypeAsync()
    {
        return Task.FromResult(typeof(TConfiguration))!;
    }

    public async Task ConfigAsync(ConfigurationBase configuration)
    {
        if (configuration is TConfiguration config)
        {
            await PerformConfigAsync(config);
        }
    }

    public async Task<IAsyncObserver<EventWrapperBase>> GetGAgentAsyncObserverAsync()
    {
        var asyncObserver = new GAgentAsyncObserver(_observers, this.GetGrainId().ToString());
        return asyncObserver;
    }

    public async Task ResumeSubscriptionAsync(IAsyncStream<EventWrapperBase> stream)
    {
        var asyncObserver = new GAgentAsyncObserver(_observers, this.GetGrainId().ToString());
        await ResumeOrSubscribeAsync(stream, asyncObserver);
    }

    protected virtual Task PerformConfigAsync(TConfiguration configuration)
    {
        return Task.CompletedTask;
    }

    [EventHandler]
    // ReSharper disable once UnusedMember.Global
    public async Task<SubscribedEventListEvent> HandleRequestAllSubscriptionsEventAsync(
        RequestAllSubscriptionsEvent request)
    {
        return await GetGroupSubscribedEventListEvent();
    }

    private async Task<SubscribedEventListEvent> GetGroupSubscribedEventListEvent()
    {
        var children = await _coordinator!.GetChildrenAsync();
        var gAgentList = children
            .Distinct()
            .Select(grainId => GrainFactory.GetGrain<IGAgent>(grainId))
            .GroupBy(g => g.GetType())
            .Select(g => g.First())
            .ToList();

        if (gAgentList.IsNullOrEmpty())
        {
            return new SubscribedEventListEvent
            {
                Value = new Dictionary<Type, List<Type>>(),
                GAgentType = GetType()
            };
        }

        if (gAgentList.Any(grain => grain == null))
        {
            throw new InvalidOperationException($"Null grains detected in GAgent List. Count: {gAgentList.Count}");
        }

        var subscriptionMap = new Dictionary<Type, List<Type>>();

        foreach (var gAgent in gAgentList)
        {
            var events = await gAgent.GetAllSubscribedEventsAsync() ?? [];
            subscriptionMap[gAgent.GetType()] = events;
        }

        return new SubscribedEventListEvent
        {
            Value = subscriptionMap,
            GAgentType = GetType()
        };
    }

    [AllEventHandler(allowSelfHandling: true)]
    protected virtual async Task ForwardEventAsync(EventWrapperBase eventWrapper)
    {
        if (eventWrapper is not EventWrapper<TEvent> typedWrapper)
        {
            Logger.LogWarning("Invalid event type received: {EventType}", eventWrapper.GetType());
            return;
        }

        using (Logger.BeginScope(new Dictionary<string, object>
               {
                   ["GrainId"] = typedWrapper.GrainId,
                   ["CorrelationId"] = typedWrapper.CorrelationId!,
                   ["PublisherGrainId"] = typedWrapper.PublisherGrainId!,
                   ["EventType"] = typeof(TEvent).Name
               }))
        {
            Logger.LogDebug("Forwarding event to children: {Event}", JsonConvert.SerializeObject(typedWrapper));
            await _coordinator!.DownwardsEventAsync(eventWrapper);
        }
    }

    protected virtual Task OnRegisterAgentAsync(GrainId agentGuid)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnRegisterAgentManyAsync(List<GrainId> agentGuids)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnUnregisterAgentAsync(GrainId agentGuid)
    {
        return Task.CompletedTask;
    }

    public abstract Task<string> GetDescriptionAsync();

    public Task<TState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public sealed override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        StateDispatcher = ServiceProvider.GetService<IStateDispatcher>();
        AevatarOptions = ServiceProvider.GetRequiredService<IOptions<AevatarOptions>>().Value;
        try
        {
            await base.OnActivateAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError("Error in OnActivateAsync.base.OnActivateAsync: {ExceptionMessage}", e.Message);
            throw;
        }

        try
        {
            await BaseOnActivateAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError("Error in OnActivateAsync.BaseOnActivateAsync: {ExceptionMessage}", e.Message);
            throw;
        }

        try
        {
            await OnGAgentActivateAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError("Error in OnActivateAsync.OnGAgentActivateAsync: {ExceptionMessage}", e.Message);
            throw;
        }
    }

    protected virtual Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        // Derived classes can override this method.
        return Task.CompletedTask;
    }

    private async Task BaseOnActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            // This must be called first to initialize Observers field.
            await UpdateObserverListAsync(GetType());
            _coordinator = GrainFactory.GetGrain<IStreamCoordinatorGrain>(
                this.GetGrainId().ToString());

            await InitializeOrResumeEventBaseStreamAsync();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in BaseOnActivateAsync: {ExceptionMessage}", e.Message);
            throw;
        }
    }

    private async Task InitializeOrResumeEventBaseStreamAsync()
    {
        if (_observers.Count == 0)
        {
            return;
        }

        try
        {
            var streamOfThisGAgent = StreamProvider.GetEventWrapperBaseStream(GrainId);
            var asyncObserver = new GAgentAsyncObserver(_observers, this.GetGrainId().ToString());
            await ResumeOrSubscribeAsync(streamOfThisGAgent, asyncObserver);
        }
        catch (Exception e)
        {
            Logger.LogError($"Error in InitializeOrResumeEventBaseStreamAsync: {e}");
            throw;
        }
    }

    private async Task ResumeOrSubscribeAsync<T>(IAsyncStream<T> stream, IAsyncObserver<T> observer)
    {
        var handles = await stream.GetAllSubscriptionHandles();
        if (handles.Count > 0)
        {
            await Task.WhenAll(handles.Select(async h => await h.ResumeAsync(observer)));
        }
        else
        {
            await stream.SubscribeAsync(observer);
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    protected virtual Task HandleStateChangedAsync()
    {
        // Derived classes can override this method.
        return Task.CompletedTask;
    }

    protected sealed override void OnStateChanged()
    {
        InternalOnStateChangedAsync().ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                Logger.LogError(task.Exception, "InternalOnStateChangedAsync operation failed");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task InternalOnStateChangedAsync()
    {
        await HandleStateChangedAsync();
        if (StateDispatcher != null)
        {
            await StateDispatcher.PublishSingleAsync(this.GetGrainId(),
                new StateWrapper<TState>(this.GetGrainId(), State, Version));
            await StateDispatcher.PublishAsync(this.GetGrainId(),
                new StateWrapper<TState>(this.GetGrainId(), State, Version));
        }
    }

    protected sealed override async void RaiseEvent<T>(T @event)
    {
        Logger.LogDebug("Base event raised: {Event}", JsonConvert.SerializeObject(@event));
        base.RaiseEvent(@event);

        AsyncTaskRunner.RunSafely(async () =>
        {
            try
            {
                await InternalRaiseEventAsync(@event);
            }
            catch (TimeoutException ex)
            {
                Logger.LogError(ex, "Event processing timeout occurred");
            }
        }, Logger);
    }

    private async Task InternalRaiseEventAsync<T>(T raisedStateLogEvent) where T : StateLogEventBase<TStateLogEvent>
    {
        await HandleRaiseEventAsync();
    }

    protected virtual Task HandleRaiseEventAsync()
    {
        // Derived classes can override this method.
        return Task.CompletedTask;
    }
}