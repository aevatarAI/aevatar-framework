using System.Collections.Concurrent;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace Aevatar.Core;



[GAgent("base")]
[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public abstract partial class
    GAgentBase<TState, TStateLogEvent>(ILogger logger) : GAgentBase<TState, TStateLogEvent, EventBase>(logger)
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>;

[GAgent("base")]
[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent> : JournaledGrain<TState, StateLogEventBase<TStateLogEvent>>, IStateGAgent<TState>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent: EventBase
{
    protected IStreamProvider StreamProvider => this.GetStreamProvider(AevatarCoreConstants.StreamProvider);

    protected readonly ILogger Logger;

    private readonly List<EventWrapperBaseAsyncObserver> _observers = [];

    private IEventDispatcher? EventDispatcher { get; set; }

    protected GAgentBase(ILogger logger)
    {
        Logger = logger;
        EventDispatcher = ServiceProvider.GetService<IEventDispatcher>();
    }

    public async Task ActivateAsync()
    {
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

    public async Task UnregisterAsync(IGAgent gAgent)
    {
        await RemoveChildAsync(gAgent.GetGrainId());
        await OnUnregisterAgentAsync(gAgent.GetPrimaryKey());
    }

    public async Task<List<Type>?> GetAllSubscribedEventsAsync(bool includeBaseHandlers = false)
    {
        var eventHandlerMethods = GetEventHandlerMethods();
        eventHandlerMethods = eventHandlerMethods.Where(m =>
            m.Name != nameof(ForwardEventAsync) && m.Name != AevatarGAgentConstants.InitializeDefaultMethodName);
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
        return State.Children;
    }

    public async Task<GrainId> GetParentAsync()
    {
        return State.Parent;
    }

    public virtual Task<Type?> GetInitializeDtoTypeAsync()
    {
        return Task.FromResult(State.InitializeDtoType);
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
            $"{this.GetGrainId().ToString()} is forwarding event downwards: {JsonConvert.SerializeObject((EventWrapper<EventBase>)eventWrapper)}");
        await SendEventDownwardsAsync((EventWrapper<EventBase>)eventWrapper);
    }

    protected virtual async Task OnRegisterAgentAsync(Guid agentGuid)
    {
    }

    protected virtual async Task OnUnregisterAgentAsync(Guid agentGuid)
    {
    }

    public abstract Task<string> GetDescriptionAsync();

    public Task<TState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public sealed override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await BaseOnActivateAsync(cancellationToken);
        await OnGAgentActivateAsync(cancellationToken);
    }

    protected virtual async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        // Derived classes can override this method.
    }

    private async Task BaseOnActivateAsync(CancellationToken cancellationToken)
    {
        // This must be called first to initialize Observers field.
        await UpdateObserverList();
        await UpdateInitializeDtoType();
        var streamOfThisGAgent = GetStream(this.GetGrainId().ToString());
        var handles = await streamOfThisGAgent.GetAllSubscriptionHandles();
        if (handles.Count > 0)
        {
            foreach (var handle in handles)
            {
                await handle.ResumeAsync(this);
            }
        }
        else
        {
            await streamOfThisGAgent.SubscribeAsync(this);
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    protected virtual async Task HandleStateChangedAsync()
    {
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
            await EventDispatcher.PublishAsync(State, this.GetGrainId().ToString());
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

    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        foreach (var observer in _observers)
        {
            await observer.OnNextAsync(item);
        }
    }

    public async Task OnCompletedAsync()
    {
        foreach (var observer in _observers)
        {
            await observer.OnCompletedAsync();
        }
    }

    public async Task OnErrorAsync(Exception ex)
    {
        foreach (var observer in _observers)
        {
            await observer.OnErrorAsync(ex);
        }
    }
}