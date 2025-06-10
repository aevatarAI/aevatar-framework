using Orleans.Streams;
using Orleans.Concurrency;

using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

public interface IBroadCastGAgent : IGAgent
{
    Task BroadCastEventAsync<T>(string streamIdString, T @event) where T : EventBase;
}

public abstract class BroadCastGAgentBase<TBroadCastState, TBroadCastStateLogEvent>
    : GAgentBase<TBroadCastState, TBroadCastStateLogEvent>, IBroadCastGAgent
        where TBroadCastState : BroadCastGState, new()
        where TBroadCastStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
{
    // For batch subscription operations
    private readonly Dictionary<string, Guid> _pendingSubscriptions = new();
    private readonly Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>> _pendingHandles = new();
    private readonly List<Task> _pendingOperations = new();

    [GenerateSerializer]
    public class SubscribeStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        [Id(0)]public required string Key { get; set; } = string.Empty;
        [Id(1)]public required Guid Value { get; set; } = Guid.Empty;
    }

    [GenerateSerializer]
    public class UnSubscribeStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        [Id(0)]public required string Key { get; set; } = string.Empty;
    }

    [GenerateSerializer]
    public class SubscribeBatchStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        [Id(0)]public required Dictionary<string, Guid> Subscriptions { get; set; } = new Dictionary<string, Guid>();
    }

    [GenerateSerializer]
    public class UnSubscribeBatchStateLogEvent : StateLogEventBase<TBroadCastStateLogEvent>
    {
        [Id(0)]public required HashSet<string> Keys { get; set; } = new HashSet<string>();
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
    /// Starts a batch subscription operation
    /// </summary>
    /// <returns>The agent instance for fluent chaining</returns>
    protected Task StartBatchSubscriptionAsync()
    {
        // Clear any previous pending operations
        _pendingSubscriptions.Clear();
        _pendingHandles.Clear();
        _pendingOperations.Clear();
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Subscribe to a broadcast event and returns the subscription handle
    /// </summary>
    protected async Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeBroadCastEventAsync<T>(string agentType, Func<T, Task> eventHandler) where T : EventBase
    {
        // Clear any previous pending operations for a single subscription
        StartBatchSubscriptionAsync();
        
        // Add the subscription
        await AddSubscriptionAsync(agentType, eventHandler);
        
        // Save and return the single handle
        var handles = await SaveBatchSubscriptionsAsync();
        if (handles.Count > 0)
        {
            return handles.Values.First();
        }
        else
        {
            throw new InvalidOperationException("No handles found");
        }
    }

    /// <summary>
    /// Adds a subscription to the current batch
    /// </summary>
    /// <typeparam name="T">The type of event to listen on</typeparam>
    /// <param name="agentType">The name of agent which published the event</param>
    /// <param name="eventHandler">The method that processes the event</param>
    /// <returns>The agent instance for fluent chaining</returns>
    protected async Task AddSubscriptionAsync<T>(string agentType, Func<T, Task> eventHandler) where T : EventBase
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long getHandlesCost = 0;
        long resumeCost = 0;
        long subscribeCost = 0;
        var stream = GenStream<T>(agentType);

        var logger = ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<EventWrapperBaseAsyncObserver>();
        if (logger == null)
        {
            Logger.LogWarning("[{0}.{1}]EventWrapperBaseAsyncObserver Logger is null", this.GetType().Name, nameof(AddSubscriptionAsync));
        }

        // Create an observer that will handle the events
        var observer = EventWrapperBaseAsyncObserver.Create(
            async item =>
            {
                var eventWrapper = item as EventWrapper<T>;
                if (eventWrapper == null)
                {
                    Logger.LogWarning("[{0}.{1}]EventWrapperBaseAsyncObserver eventWrapper is null", this.GetType().Name, nameof(AddSubscriptionAsync));
                    return;
                }

                await eventHandler.Invoke(eventWrapper.Event);
            }, ServiceProvider, eventHandler.Method.Name, typeof(T).Name);

        var key = GetStreamIdString<T>(agentType);

        StreamSubscriptionHandle<EventWrapperBase> handle;
        
        if (State.Subscription.TryGetValue(key, out Guid handleId))
        {
            Logger.LogWarning("[{0}.{1}]Subscription {2} already exists", this.GetType().Name, nameof(AddSubscriptionAsync), key);
            var getHandlesStart = stopwatch.ElapsedMilliseconds;
            var handles = await stream.GetAllSubscriptionHandles();
            getHandlesCost = stopwatch.ElapsedMilliseconds - getHandlesStart;
            var resumeHandles = handles.Where(h => h.HandleId == handleId).ToList();
            if (resumeHandles.IsNullOrEmpty())
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be resumed, continue to subscribe", this.GetType().Name, nameof(AddSubscriptionAsync), handleId);
            }
            else if (resumeHandles.Count > 1)
            {
                Logger.LogError("[{0}.{1}]Multiple handles found for {2} to be resumed", this.GetType().Name, nameof(AddSubscriptionAsync), handleId);
                throw new InvalidOperationException($"Multiple handles found for {handleId} to be resumed");
            }
            else
            {
                var resumeStart = stopwatch.ElapsedMilliseconds;
                handle = await resumeHandles.First().ResumeAsync(observer);
                resumeCost = stopwatch.ElapsedMilliseconds - resumeStart;
                _pendingHandles[key] = handle; // Only update _pendingHandles for resume
                stopwatch.Stop();
                Logger.LogDebug("[{0}.{1} AddSubscriptionAsync]Timing: GetAllSubscriptionHandles: {2}ms, ResumeAsync: {3}ms", this.GetType().Name, nameof(AddSubscriptionAsync), getHandlesCost, resumeCost);
                return;
            }
        }

        var subscribeStart = stopwatch.ElapsedMilliseconds;
        handle = await stream.SubscribeAsync(observer);
        subscribeCost = stopwatch.ElapsedMilliseconds - subscribeStart;
        Logger.LogInformation("[{0}.{1}]Subscription {2} created", this.GetType().Name, nameof(AddSubscriptionAsync), key);
        
        // Add to pending collections
        _pendingSubscriptions[key] = handle.HandleId;
        _pendingHandles[key] = handle;
        stopwatch.Stop();
        Logger.LogDebug("[{0}.{1} AddSubscriptionAsync]Timing: GetAllSubscriptionHandles: {2}ms, ResumeAsync: {3}ms, SubscribeAsync: {4}ms", this.GetType().Name, nameof(AddSubscriptionAsync), getHandlesCost, resumeCost, subscribeCost);
    }

    /// <summary>
    /// Saves all pending subscriptions in the current batch with a single database write
    /// </summary>
    /// <returns>Dictionary of handles by key (agentType.eventType)</returns>
    protected async Task<Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>>> SaveBatchSubscriptionsAsync()
    {
        if (!_pendingSubscriptions.Any())
        {
            Logger.LogWarning("[{0}.{1}]No pending subscriptions to save", this.GetType().Name, nameof(SaveBatchSubscriptionsAsync));
            return new Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>>(_pendingHandles);
        }

        var subscribeBatchEvent = new SubscribeBatchStateLogEvent
        {
            Subscriptions = new Dictionary<string, Guid>(_pendingSubscriptions)
        };
        
        RaiseEvent(subscribeBatchEvent);
        await ConfirmEvents();
        
        Logger.LogInformation("[{0}.{1}]Saved {2} subscriptions", this.GetType().Name, nameof(SaveBatchSubscriptionsAsync), _pendingSubscriptions.Count);
        
        // Return a copy of the handles to the caller
        return new Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>>(_pendingHandles);
    }

    /// <summary>
    /// Unsubscribes from multiple broadcast event streams in a batch
    /// </summary>
    /// <typeparam name="T">The type of event listened to</typeparam>
    /// <param name="subscriptions">Dictionary containing grain types and their corresponding handles to unsubscribe</param>
    /// <returns></returns>
    protected async Task UnSubscribeBroadCastEventsAsync<T>(Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>> subscriptions) where T : EventBase
    {
        if (subscriptions == null || !subscriptions.Any())
        {
            return;
        }

        var keysToUnsubscribe = new HashSet<string>();
        
        // Create streams and get subscription handles for all grain types once
        var grTypeToStreamAndHandles = new Dictionary<string, (IAsyncStream<EventWrapperBase> Stream, List<StreamSubscriptionHandle<EventWrapperBase>> Handles)>();
        
        foreach (var grType in subscriptions.Keys)
        {
            var stream = GenStream<T>(grType);
            var handles = await stream.GetAllSubscriptionHandles();
            grTypeToStreamAndHandles[grType] = (stream, handles.ToList());
        }

        foreach (var subscription in subscriptions)
        {
            var grType = subscription.Key;
            var handle = subscription.Value;
            
            if (!grTypeToStreamAndHandles.TryGetValue(grType, out var streamAndHandles))
            {
                Logger.LogError("[{0}.{1}]Failed to get stream and handles for grain type {2}", this.GetType().Name, nameof(UnSubscribeBroadCastEventsAsync), grType);
                continue;
            }
            
            var stream = streamAndHandles.Stream;
            var handles = streamAndHandles.Handles;

            var unsub = handles.Where(x => x.HandleId == handle.HandleId).ToList();
            
            if (unsub.IsNullOrEmpty())
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastEventsAsync), handle.HandleId);
                continue;
            }

            if (unsub.Count > 1)
            {
                Logger.LogWarning("[{0}.{1}]Multiple handles found for {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastEventsAsync), handle.HandleId);
            }

            foreach (var unsubscription in unsub)
            {
                await unsubscription.UnsubscribeAsync();
            }
            
            var key = GetStreamIdString<T>(grType);
            if (State.Subscription.ContainsKey(key))
            {
                keysToUnsubscribe.Add(key);
                Logger.LogInformation("[{0}.{1}]Unsubscribed from {2}", this.GetType().Name, nameof(UnSubscribeBroadCastEventsAsync), key);
            }
            else
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate key {2} in state to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastEventsAsync), key);
            }
        }

        // Batch unsubscribe all keys in a single event
        if (keysToUnsubscribe.Any())
        {
            var unsubscribeBatchEvent = new UnSubscribeBatchStateLogEvent
            {
                Keys = keysToUnsubscribe
            };
            RaiseEvent(unsubscribeBatchEvent);
            await ConfirmEvents();
        }
    }

    /// <summary>
    /// Unsubscribes from the broadcast event stream
    /// </summary>
    /// <typeparam name="T">The type of event to be listen on</typeparam>
    /// <param name="grType">The name of agent which published the event</param>
    /// <param name="handle"></param>
    /// <returns></returns>
    protected async Task UnSubscribeBroadCastAsync<T>(string grType, StreamSubscriptionHandle<EventWrapperBase> handle) where T : EventBase
    {
        var stream = GenStream<T>(grType);

        var handles = await stream.GetAllSubscriptionHandles();
        var unsub = handles.Where(x => x.HandleId == handle.HandleId).ToList();
        if (unsub.IsNullOrEmpty())
        {
            Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handle.HandleId);
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

    protected async Task UnSubscribeBroadCastAsync<T>(string grType) where T : EventBase
    {
        var key = GetStreamIdString<T>(grType);

        if (State.Subscription.TryGetValue(key, out Guid handleId))
        {
            var stream = GenStream<T>(grType);
            var handles = await stream.GetAllSubscriptionHandles();
            var unsub = handles.Where(x => x.HandleId == handleId).ToList();

            if (unsub.IsNullOrEmpty())
            {
                Logger.LogWarning("[{0}.{1}]Unable to locate handle {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handleId);
            }

            if (unsub.Count > 1)
            {
                Logger.LogWarning("[{0}.{1}]Multiple handles found for {2} to be unsubscribed", this.GetType().Name, nameof(UnSubscribeBroadCastAsync), handleId);
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
            case SubscribeBatchStateLogEvent subscribeBatchStateLogEvent:
                foreach (var item in subscribeBatchStateLogEvent.Subscriptions)
                {
                    state.Subscription[item.Key] = item.Value;
                }
                break;
            case UnSubscribeBatchStateLogEvent unSubscribeBatchStateLogEvent:
                foreach (var key in unSubscribeBatchStateLogEvent.Keys)
                {
                    state.Subscription.Remove(key);
                }
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