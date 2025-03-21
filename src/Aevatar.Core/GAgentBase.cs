using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Orleans.EventSourcing;
using Orleans.Providers;
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
    : JournaledGrain<TState, StateLogEventBase<TStateLogEvent>>, IStateGAgent<TState>
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

    public async Task ActivateAsync()
    {
        await Task.Yield();
    }

    public async Task RegisterAsync(IGAgent gAgent)
    {
        if (gAgent.GetGrainId() == this.GetGrainId())
        {
            Logger.LogError($"Cannot register GAgent with same GrainId.");
            return;
        }

        await AddChildAsync(gAgent.GetGrainId());
        await gAgent.SubscribeToAsync(this);
        await OnRegisterAgentAsync(gAgent.GetGrainId());
    }

    public async Task SubscribeToAsync(IGAgent gAgent)
    {
        var parentGrainId = gAgent.GetGrainId();
        var parentStream = GetEventBaseStreamForChildren(parentGrainId);
        var asyncObserver = new GAgentAsyncObserver(_observers);
        await ResumeOrSubscribeAsync(parentStream, asyncObserver);
        await SetParentAsync(gAgent.GetGrainId());
    }

    public async Task UnsubscribeFromAsync(IGAgent gAgent)
    {
        await ClearParentAsync(gAgent.GetGrainId());
    }

    public async Task UnregisterAsync(IGAgent gAgent)
    {
        await RemoveChildAsync(gAgent.GetGrainId());
        await gAgent.UnsubscribeFromAsync(this);
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

    public Task<List<GrainId>> GetChildrenAsync()
    {
        return Task.FromResult(State.Children);
    }

    public Task<GrainId> GetParentAsync()
    {
        return Task.FromResult(State.Parent ?? default);
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
        var asyncObserver = new GAgentAsyncObserver(_observers);
        return asyncObserver;
    }

    public async Task ResumeSubscriptionAsync(IAsyncStream<EventWrapperBase> stream)
    {
        var asyncObserver = new GAgentAsyncObserver(_observers);
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
        var gAgentList = State.Children
            .Distinct()
            .Select(grainId => GrainFactory.GetGrain<IGAgent>(grainId))
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
            await SendEventDownwardsAsync(typedWrapper);
        }
    }

    protected virtual Task OnRegisterAgentAsync(GrainId agentGuid)
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
        await base.OnActivateAsync(cancellationToken);
        await BaseOnActivateAsync(cancellationToken);
        await OnGAgentActivateAsync(cancellationToken);
    }

    protected virtual Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        // Derived classes can override this method.
        return Task.CompletedTask;
    }

    private async Task BaseOnActivateAsync(CancellationToken cancellationToken)
    {
        // This must be called first to initialize Observers field.
        await UpdateObserverListAsync(GetType());

        var initTasks = new[]
        {
            InitializeOrResumeEventBaseStreamAsync(),
            ResumeEventBaseStreamForChildrenAsync(),
            ActivateProjectionGrainAsync()
        };
        await Task.WhenAll(initTasks);
    }

    private async Task InitializeOrResumeEventBaseStreamAsync()
    {
        var streamOfThisGAgent = GetEventBaseStreamForOwn(this.GetGrainId());
        var asyncObserver = new GAgentAsyncObserver(_observers);
        await ResumeOrSubscribeAsync(streamOfThisGAgent, asyncObserver);
    }

    private async Task ResumeEventBaseStreamForChildrenAsync()
    {
        var streamForChildren = GetEventBaseStreamForChildren(this.GetGrainId());
        var tasks = new List<Task>();
        foreach (var childGrainId in State.Children)
        {
            var child = await GAgentFactory.GetGAgentAsync(childGrainId);
            tasks.Add(child.ResumeSubscriptionAsync(streamForChildren));
        }

        await tasks.WhenAll();
    }

    private async Task ActivateProjectionGrainAsync()
    {
        var projectionGrain = GrainFactory.GetGrain<IProjectionGrain>(typeof(TState).FullName);
        await projectionGrain.ActivateAsync();
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

    private IAsyncStream<EventWrapperBase> GetEventBaseStreamForOwn(GrainId grainId)
    {
        var grainIdString = grainId.ToString();
        return GetEventBaseStream(grainIdString);
    }

    private IAsyncStream<EventWrapperBase> GetEventBaseStreamForChildren(GrainId grainId)
    {
        var grainIdString = $"{grainId.ToString()}/Children";
        return GetEventBaseStream(grainIdString);
    }

    private IAsyncStream<EventWrapperBase> GetEventBaseStream(string streamKey)
    {
        var streamId = StreamId.Create(AevatarOptions!.StreamNamespace, streamKey);
        return StreamProvider.GetStream<EventWrapperBase>(streamId);
    }
}