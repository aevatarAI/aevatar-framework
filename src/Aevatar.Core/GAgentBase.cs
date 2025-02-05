using System.Collections.Concurrent;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
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
    protected IStreamProvider StreamProvider => this.GetStreamProvider(AevatarCoreConstants.StreamProvider);

    public ILogger Logger { get; set; } = NullLogger.Instance;

    private readonly List<EventWrapperBaseAsyncObserver> _observers = [];

    private IEventDispatcher? EventDispatcher { get; set; }

    protected GAgentBase()
    {
        EventDispatcher = ServiceProvider.GetService<IEventDispatcher>();
    }

    public async Task ActivateAsync()
    {
        await Task.Yield();
    }

    public async Task RegisterAsync(IGAgent gAgent)
    {
        var guid = gAgent.GetPrimaryKey();
        if (gAgent.GetGrainId() == this.GetGrainId())
        {
            Logger.LogError($"Cannot register GAgent with same GrainId.");
            return;
        }

        await AddChildAsync(gAgent.GetGrainId());
        await gAgent.SubscribeToAsync(this);
        await OnRegisterAgentAsync(guid);
    }

    public Task SubscribeToAsync(IGAgent gAgent)
    {
        return SetParentAsync(gAgent.GetGrainId());
    }

    public Task UnsubscribeFromAsync(IGAgent gAgent)
    {
        return ClearParentAsync(gAgent.GetGrainId());
    }

    public async Task UnregisterAsync(IGAgent gAgent)
    {
        await RemoveChildAsync(gAgent.GetGrainId());
        await gAgent.UnsubscribeFromAsync(this);
        await OnUnregisterAgentAsync(gAgent.GetPrimaryKey());
    }

    public virtual Task<List<Type>?> GetAllSubscribedEventsAsync(bool includeBaseHandlers = false)
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

        return Task.FromResult(handlingTypes.ToList())!;
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

    protected virtual Task PerformConfigAsync(TConfiguration configuration)
    {
        return Task.CompletedTask;
    }

    [EventHandler]
    public async Task<SubscribedEventListEvent> HandleRequestAllSubscriptionsEventAsync(
        RequestAllSubscriptionsEvent request)
    {
        return await GetGroupSubscribedEventListEvent();
    }

    private async Task<SubscribedEventListEvent> GetGroupSubscribedEventListEvent()
    {
        var gAgentList = State.Children.Select(grainId => GrainFactory.GetGrain<IGAgent>(grainId)).ToList();

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
            // Only happened on test environment.
            throw new InvalidOperationException("One or more grains in gAgentList are null.");
        }

        var dict = new ConcurrentDictionary<Type, List<Type>>();
        foreach (var gAgent in gAgentList.AsParallel())
        {
            var eventList = await gAgent.GetAllSubscribedEventsAsync();
            dict[gAgent.GetType()] = eventList ?? [];
        }

        return new SubscribedEventListEvent
        {
            Value = dict.ToDictionary(),
            GAgentType = GetType()
        };
    }

    [AllEventHandler]
    internal async Task ForwardEventAsync(EventWrapperBase eventWrapper)
    {
        Logger.LogInformation(
            $"{this.GetGrainId().ToString()} is forwarding event downwards: {JsonConvert.SerializeObject((EventWrapper<TEvent>)eventWrapper)}");
        await SendEventDownwardsAsync((EventWrapper<TEvent>)eventWrapper);
    }

    protected virtual Task OnRegisterAgentAsync(Guid agentGuid)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnUnregisterAgentAsync(Guid agentGuid)
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
        await InitializeOrResumeStreamAsync();
    }

    private async Task InitializeOrResumeStreamAsync()
    {
        var streamOfThisGAgent = GetStream(this.GetGrainId().ToString());
        var handles = await streamOfThisGAgent.GetAllSubscriptionHandles();
        var asyncObserver = new GAgentAsyncObserver(_observers);
        if (handles.Count > 0)
        {
            foreach (var handle in handles)
            {
                await handle.ResumeAsync(asyncObserver);
            }
        }
        else
        {
            await streamOfThisGAgent.SubscribeAsync(asyncObserver);
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    protected virtual Task HandleStateChangedAsync()
    {
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
        //TODO:  need optimize use kafka,ensure Es written successfully
        if (EventDispatcher != null)
        {
            await EventDispatcher.PublishAsync(State, this.GetGrainId());
        }
    }

    protected sealed override async void RaiseEvent<T>(T @event)
    {
        Logger.LogInformation("base raiseEvent info:{info}", JsonConvert.SerializeObject(@event));
        base.RaiseEvent(@event);
        InternalRaiseEventAsync(@event).ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                Logger.LogError(task.Exception, "InternalRaiseEventAsync operation failed");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task InternalRaiseEventAsync<T>(T @event)
    {
        await HandleRaiseEventAsync();
        //TODO:  need optimize use kafka,ensure Es written successfully
        var gEvent = @event as StateLogEventBase;
        if (EventDispatcher != null)
        {
            await EventDispatcher.PublishAsync(gEvent!.Id, this.GetGrainId(), gEvent);
        }
    }

    protected virtual async Task HandleRaiseEventAsync()
    {

    }

    private IAsyncStream<EventWrapperBase> GetStream(string grainIdString)
    {
        var streamId = StreamId.Create(AevatarCoreConstants.StreamNamespace, grainIdString);
        return StreamProvider.GetStream<EventWrapperBase>(streamId);
    }
}