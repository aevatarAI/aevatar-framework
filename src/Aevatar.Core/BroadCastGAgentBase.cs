using Orleans.Streams;
using Orleans.Concurrency;

using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public interface IBroadCastGAgent : IGAgent
{
    Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string grainType, Func<T, Task> eventHandler) where T : EventBase;

    Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle) where T : EventBase;

    Task BroadCastEventAsync<T>(string streamIdString, T @event) where T : EventBase;
}

public abstract class BroadCastGAgentBase<TBroadCastState, TBroadCastStateLogEvent>
    : GAgentBase<TBroadCastState, TBroadCastStateLogEvent>, IBroadCastGAgent
        where TBroadCastState : BroadCastGState, new()
        where TBroadCastStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
{
    [GenerateSerializer]
    public class SubscribeStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        public required string Key { get; set; } = string.Empty;
        public required StreamSubscriptionHandle<EventWrapperBase> Value { get; set; } = null!;
    }

    [GenerateSerializer]
    public class UnSubscribeStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        public required string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// Returns the description of the agent
    /// </summary>
    /// <returns></returns>
    [ReadOnly]
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is an agent that used to manage publishing and subscribing of the broadcast events.");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="streamIdString"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    public async Task BroadCastEventAsync<T>(string streamIdString, T @event) where T : EventBase
    {
        var stream = GenStream<T>(streamIdString);
        var eventWrapper = new EventWrapper<T>(@event, Guid.NewGuid(), this.GetGrainId());
        await stream.OnNextAsync(eventWrapper);
    }

    /// <summary>
    /// Subscribes to the broadcast event stream
    /// The stream id string is the combination of grain type and event type
    /// </summary>
    /// <typeparam name="T">The type of event to be listen on</typeparam>
    /// <param name="grType">The name of agent which published the event</param>
    /// <param name="eventHandler">The method that process the event</param>
    /// <returns>Event handle which can be used later. E.g. unsubscribe</returns>
    public async Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string grType, Func<T, Task> eventHandler) where T : EventBase
    {
        var stream = GenStream<T>(grType);

        var logger = ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<EventWrapperBaseAsyncObserver>();
        if (logger == null)
        {
            Logger.LogWarning("[{0}.{1}]EventWrapperBaseAsyncObserver Logger is null", this.GetType().Name, nameof(SubscribeBroadCastEventAsync));
        }

        // Create an observer that will handle the events
        var observer = EventWrapperBaseAsyncObserver.Create(
            async item =>
            {
                var eventWrapper = item as EventWrapper<T>;
                if (eventWrapper == null)
                {
                    Logger.LogWarning("[{0}.{1}]EventWrapperBaseAsyncObserver eventWrapper is null", this.GetType().Name, nameof(SubscribeBroadCastEventAsync));
                    return;
                }

                await eventHandler.Invoke(eventWrapper.Event);
            }, ServiceProvider, typeof(EventHandler).Name, typeof(T).Name);

        var key = GetStreamIdString<T>(grType);

        if (State.Subscription.TryGetValue(key, out StreamSubscriptionHandle<EventWrapperBase>? value))
        {
            Logger.LogWarning("[{0}.{1}]SubscribeBroadCastEventAsync {2} already exists", this.GetType().Name, nameof(SubscribeBroadCastEventAsync), key);
            var resumeHandle = await value.ResumeAsync(observer);
            return resumeHandle;
        }

        var handle = await stream.SubscribeAsync(observer);
        var subscribeEvent = new SubscribeStateLogEvent
        {
            Key = key,
            Value = handle
        };
        RaiseEvent(subscribeEvent);
        await ConfirmEvents();
        return handle;
    }

    /// <summary>
    /// Unsubscribes from the broadcast event stream
    /// </summary>
    /// <typeparam name="T">The type of event to be listen on</typeparam>
    /// <param name="grType">The name of agent which published the event</param>
    /// <param name="handle"></param>
    /// <returns></returns>
    public async Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle) where T : EventBase
    {
        var stream = GenStream<T>(grType);

        var handles = await stream.GetAllSubscriptionHandles();
        var unsub = handles.Where(x => x.HandleId == handle.HandleId).ToList();
        if (unsub.IsNullOrEmpty())
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {3} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handle.HandleId);
            return;
        }

        if (unsub.Count > 1)
        {
            Logger.LogWarning("[{0}.{1}]Multiple handles found for {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handle.HandleId);
        }

        foreach (var subscription in unsub)
        {
            await subscription.UnsubscribeAsync();
        }
        var key = GetStreamIdString<T>(grType);

        if (State.Subscription.ContainsKey(key))
        {
            var unsubscribeEvent = new UnSubscribeStateLogEvent
            {
                Key = key
            };
            RaiseEvent(unsubscribeEvent);
            await ConfirmEvents();
            Logger.LogInformation("[{0}.{1}]Unsubscribed from {2}", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), key);
        }
        else
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), key);
        }
    }

    public async Task UnSubscribeBroadCastAsync<T>(string grType) where T : EventBase
    {
        var key = GetStreamIdString<T>(grType);

        if (State.Subscription.TryGetValue(key, out StreamSubscriptionHandle<EventWrapperBase>? handle))
        {
            var stream = GenStream<T>(grType);
            var handles = await stream.GetAllSubscriptionHandles();
            var unsub = handles.Where(x => x.HandleId == handle.HandleId).ToList();

            if (unsub.IsNullOrEmpty())
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate handle {3} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handle.HandleId);
            }

            if (unsub.Count > 1)
            {
                Logger.LogWarning("[{0}.{1}]Multiple handles found for {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handle.HandleId);
            }

            foreach (var subscription in unsub)
            {
                await subscription.UnsubscribeAsync();
            }
            var unsubscribeEvent = new UnSubscribeStateLogEvent
            {
                Key = key
            };
            RaiseEvent(unsubscribeEvent);
            await ConfirmEvents();
            Logger.LogInformation("[{0}.{1}]Unsubscribed from {2}", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), key);
        }
        else
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), key);
        }
    }

    protected override void GAgentTransitionState(TBroadCastState state, StateLogEventBase<TBroadCastStateLogEvent> @event)
    {
        switch (@event)
        {
            case SubscribeStateLogEvent subscribeStateLogEvent:
                state.Subscription.Add(subscribeStateLogEvent.Key, subscribeStateLogEvent.Value);
                break;
            case UnSubscribeStateLogEvent unSubscribeStateLogEvent:
                state.Subscription.Remove(unSubscribeStateLogEvent.Key);
                break;
        }
        //call base class to handle the state transition if any
        base.GAgentTransitionState(state, @event);
    }

    private IAsyncStream<EventWrapperBase> GenStream<T>(string grType)
    {
        var streamIdString = GetStreamIdString<T>(grType);
        var streamId = StreamId.Create(AevatarOptions!.BroadCastStreamNamespace, streamIdString);
        return StreamProvider.GetStream<EventWrapperBase>(streamId);
    }

    private string GetStreamIdString<T>(string grType)
    {
        var streamIdString = grType + "." + typeof(T).Name;
        Logger.LogInformation("[{0}.{1}]StreamIdString: {2}", this.GetType().Name, nameof(GetStreamIdString), streamIdString);
        return streamIdString;
    }
}