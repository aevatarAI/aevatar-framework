using Orleans.Streams;
using Orleans.Concurrency;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public interface IBroadCastGAgent : IGAgent
{
    Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string grainType,
        Func<T, Task> eventHandler) where T : EventBase;

    Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle)
        where T : EventBase;

    // Task UnSubscribeBroadCastAsync<T>(string grType) where T : EventBase;

    Task BroadCastEventAsync<T>(string streamIdString, T @event) where T : EventBase;

    Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string grainType,
        Func<T, Task> eventHandler, string? customTopic = null) where T : EventBase;

    Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle,
        string? customTopic = null) where T : EventBase;

    Task BroadCastEventAsync<T>(string streamIdString, T @event, string? customTopic = null) where T : EventBase;

    Task UnSubscribeBroadCastByTopicAsync<T>(string grType, string? customTopic = null) where T : EventBase;
}

public abstract class BroadCastGAgentBase<TBroadCastState, TBroadCastStateLogEvent>
    : GAgentBase<TBroadCastState, TBroadCastStateLogEvent>, IBroadCastGAgent
    where TBroadCastState : BroadCastGState, new()
    where TBroadCastStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
{
    [GenerateSerializer]
    public class SubscribeStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        [Id(0)] public required string Key { get; set; } = string.Empty;
        [Id(1)] public required Guid Value { get; set; } = Guid.Empty;
    }

    [GenerateSerializer]
    public class UnSubscribeStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        [Id(0)] public required string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// Returns the description of the agent
    /// </summary>
    /// <returns></returns>
    [ReadOnly]
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult(
            "This is an agent that used to manage publishing and subscribing of the broadcast events.");
    }

    /// <summary>
    /// Broadcast an event to the default topic
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="streamIdString">Stream ID string</param>
    /// <param name="event">Event object</param>
    /// <returns></returns>
    public async Task BroadCastEventAsync<T>(string streamIdString, T @event) where T : EventBase
    {
        await BroadCastEventAsync(streamIdString, @event, null);
    }

    /// <summary>
    /// Broadcast an event to the specified topic
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="streamIdString">Stream ID string</param>
    /// <param name="event">Event object</param>
    /// <param name="customTopic">Custom topic name, uses default topic when null</param>
    /// <returns></returns>
    public async Task BroadCastEventAsync<T>(string streamIdString, T @event, string? customTopic = null)
        where T : EventBase
    {
        var stream = GenStream<T>(streamIdString, customTopic);
        var eventWrapper = new EventWrapper<T>(@event, Guid.NewGuid(), this.GetGrainId());
        await stream.OnNextAsync(eventWrapper);
    }

    /// <summary>
    /// Subscribe to broadcast events (using default topic)
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type that publishes the event</param>
    /// <param name="eventHandler">Event handler function</param>
    /// <returns>Handle that can be used to unsubscribe</returns>
    public async Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string grType,
        Func<T, Task> eventHandler) where T : EventBase
    {
        return await SubscribeBroadCastEventAsync<T>(grType, eventHandler, null);
    }

    /// <summary>
    /// Subscribe to broadcast events
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type that publishes the event</param>
    /// <param name="eventHandler">Event handler function</param>
    /// <param name="customTopic">Custom topic name, uses default topic when null</param>
    /// <returns>Handle that can be used to unsubscribe</returns>
    public async Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string grType,
        Func<T, Task> eventHandler, string? customTopic = null) where T : EventBase
    {
        var stream = GenStream<T>(grType, customTopic);

        var logger = ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<EventWrapperBaseAsyncObserver>();
        if (logger == null)
        {
            Logger.LogWarning("[{0}.{1}]EventWrapperBaseAsyncObserver Logger is null", this.GetType().Name,
                nameof(SubscribeBroadCastEventAsync));
        }

        // Create an observer that will handle the events
        var observer = EventWrapperBaseAsyncObserver.Create(
            async item =>
            {
                var eventWrapper = item as EventWrapper<T>;
                if (eventWrapper == null)
                {
                    Logger.LogWarning("[{0}.{1}]EventWrapperBaseAsyncObserver eventWrapper is null",
                        this.GetType().Name, nameof(SubscribeBroadCastEventAsync));
                    return;
                }

                await eventHandler.Invoke(eventWrapper.Event);
            }, ServiceProvider, eventHandler.Method.Name, typeof(T).Name);

        var key = GetStreamIdString<T>(grType, customTopic);

        if (State.Subscription.TryGetValue(key, out Guid handleId))
        {
            Logger.LogWarning("[{0}.{1}]SubscribeBroadCastEventAsync {2} already exists", this.GetType().Name,
                nameof(SubscribeBroadCastEventAsync), key);
            var handles = await stream.GetAllSubscriptionHandles();
            var resumeHandles = handles.Where(h => h.HandleId == handleId).ToList();
            if (resumeHandles.IsNullOrEmpty())
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate handle {3} to be resumed, continue to subscribe",
                    this.GetType().Name, nameof(SubscribeBroadCastEventAsync), handleId);
                var unsubscribeEvent = new UnSubscribeStateLogEvent
                {
                    Key = key
                };
                RaiseEvent(unsubscribeEvent);
                await ConfirmEvents();
            }
            else if (resumeHandles.Count > 1)
            {
                Logger.LogError("[{0}.{1}]Multiple handles found for {2} to be resumed", this.GetType().Name,
                    nameof(SubscribeBroadCastEventAsync), handleId);
                throw new InvalidOperationException($"Multiple handles found for {handleId} to be resumed");
            }
            else
            {
                return await resumeHandles.First().ResumeAsync(observer);
            }
        }

        var handle = await stream.SubscribeAsync(observer);
        Logger.LogInformation("[{0}.{1}]SubscribeBroadCastEventAsync {2} created", this.GetType().Name,
            nameof(SubscribeBroadCastEventAsync), key);

        var subscribeEvent = new SubscribeStateLogEvent
        {
            Key = key,
            Value = handle.HandleId
        };
        RaiseEvent(subscribeEvent);
        await ConfirmEvents();
        return handle;
    }

    /// <summary>
    /// Unsubscribe from broadcast events (using default topic)
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type that publishes the event</param>
    /// <param name="handle">Subscription handle</param>
    /// <returns></returns>
    public async Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle)
        where T : EventBase
    {
        await UnSubscribeBroadCastAsync<T>(grType, handle, null);
    }

    /// <summary>
    /// Unsubscribe from broadcast events
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type that publishes the event</param>
    /// <param name="handle">Subscription handle</param>
    /// <param name="customTopic">Custom topic name, uses default topic when null</param>
    /// <returns></returns>
    public async Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle,
        string? customTopic = null) where T : EventBase
    {
        var stream = GenStream<T>(grType, customTopic);

        var handles = await stream.GetAllSubscriptionHandles();
        var unsub = handles.Where(x => x.HandleId == handle.HandleId).ToList();
        if (unsub.IsNullOrEmpty())
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {3} to be unsubscribed", this.GetType().Name,
                nameof(UnSubscribeBroadCastAsync), handle.HandleId);
            return;
        }

        if (unsub.Count > 1)
        {
            Logger.LogWarning("[{0}.{1}]Multiple handles found for {2} to be unsubscribed", this.GetType().Name,
                nameof(UnSubscribeBroadCastAsync), handle.HandleId);
        }

        foreach (var subscription in unsub)
        {
            await subscription.UnsubscribeAsync();
        }

        var key = GetStreamIdString<T>(grType, customTopic);

        if (State.Subscription.ContainsKey(key))
        {
            var unsubscribeEvent = new UnSubscribeStateLogEvent
            {
                Key = key
            };
            RaiseEvent(unsubscribeEvent);
            await ConfirmEvents();
            Logger.LogInformation("[{0}.{1}]Unsubscribed from {2}", this.GetType().Name,
                nameof(UnSubscribeBroadCastAsync), key);
        }
        else
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name,
                nameof(UnSubscribeBroadCastAsync), key);
        }
    }

    /// <summary>
    /// Unsubscribe using the default topic and event type
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type</param>
    /// <returns></returns>
    public async Task UnSubscribeBroadCastAsync<T>(string grType) where T : EventBase
    {
        await UnSubscribeBroadCastByTopicAsync<T>(grType, null);
    }

    /// <summary>
    /// Unsubscribe using the specified topic and event type
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type</param>
    /// <param name="customTopic">Custom topic, uses default topic when null</param>
    /// <returns></returns>
    public async Task UnSubscribeBroadCastByTopicAsync<T>(string grType, string? customTopic = null) where T : EventBase
    {
        var key = GetStreamIdString<T>(grType, customTopic);

        if (State.Subscription.TryGetValue(key, out Guid handleId))
        {
            var stream = GenStream<T>(grType, customTopic);
            var handles = await stream.GetAllSubscriptionHandles();
            var unsub = handles.Where(x => x.HandleId == handleId).ToList();

            if (unsub.IsNullOrEmpty())
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate handle {3} to be unsubscribed", this.GetType().Name,
                    nameof(UnSubscribeBroadCastByTopicAsync), handleId);
            }

            if (unsub.Count > 1)
            {
                Logger.LogWarning("[{0}.{1}]Multiple handles found for {2} to be unsubscribed", this.GetType().Name,
                    nameof(UnSubscribeBroadCastByTopicAsync), handleId);
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
            Logger.LogInformation("[{0}.{1}]Unsubscribed from {2}", this.GetType().Name,
                nameof(UnSubscribeBroadCastByTopicAsync), key);
        }
        else
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name,
                nameof(UnSubscribeBroadCastByTopicAsync), key);
        }
    }

    protected override void GAgentTransitionState(TBroadCastState state,
        StateLogEventBase<TBroadCastStateLogEvent> @event)
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

    /// <summary>
    /// Generate event stream (using default or custom topic)
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type</param>
    /// <param name="customTopic">Custom topic, uses default topic when null</param>
    /// <returns>Event stream</returns>
    private IAsyncStream<EventWrapperBase> GenStream<T>(string grType, string? customTopic = null)
    {
        var streamIdString = GetStreamIdString<T>(grType, customTopic);
        var namespace_ = !string.IsNullOrEmpty(customTopic) ? customTopic : AevatarOptions!.BroadCastStreamNamespace;
        var streamId = StreamId.Create(namespace_, streamIdString);
        Logger.LogInformation("[{0}.{1}]Creating stream with namespace {2} and id {3}",
            this.GetType().Name, nameof(GenStream), namespace_, streamIdString);
        return StreamProvider.GetStream<EventWrapperBase>(streamId);
    }

    /// <summary>
    /// Get stream ID string
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="grType">Agent type</param>
    /// <param name="customTopic">Custom topic, uses default format when null</param>
    /// <returns>Stream ID string</returns>
    private string GetStreamIdString<T>(string grType, string? customTopic = null)
    {
        var streamIdString =
            customTopic.IsNullOrEmpty() == true ? "" : $"{customTopic}." + grType + "." + typeof(T).Name;
        Logger.LogInformation("[{0}.{1}]StreamIdString: {2} with topic: {3}",
            this.GetType().Name, nameof(GetStreamIdString), streamIdString,
            customTopic ?? AevatarOptions!.BroadCastStreamNamespace);
        return streamIdString;
    }
}