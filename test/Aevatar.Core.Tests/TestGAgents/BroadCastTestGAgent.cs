using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Aevatar.Core.Tests.TestGAgents;

// Test broadcast event
[GenerateSerializer]
public class BroadCastTestEvent : EventBase
{
    [Id(0)] public string Message { get; set; } = string.Empty;
}

// Test agent state class
[GenerateSerializer]
public class BroadCastTestGAgentState : BroadCastGState
{
    [Id(1)] public List<string> ReceivedMessages { get; set; } = new();
}

// State log event class
public class BroadCastTestStateLogEvent : StateLogEventBase<BroadCastTestStateLogEvent>;

// Add message state log event
[GenerateSerializer]
public class AddMessageStateLogEvent : StateLogEventBase<BroadCastTestStateLogEvent>
{
    [Id(0)] public required string Message { get; set; } = string.Empty;
}

[GAgent("broadcastTest", "test")]
public class BroadCastTestGAgent : BroadCastGAgentBase<BroadCastTestGAgentState, BroadCastTestStateLogEvent>, IBroadCastTestGAgent
{
    private StreamSubscriptionHandle<EventWrapperBase>? _handle;

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used for testing broadcast events subscription and publishing.");
    }

    // Publish broadcast test event
    public async Task PublishBroadCastTestEventAsync(string message, string? customTopic = null)
    {
        var @event = new BroadCastTestEvent { Message = message };
        await BroadCastEventAsync(this.GetType().FullName!, @event, customTopic);
        Logger.LogInformation("Published broadcast event with message: {Message}", message);
    }

    // Subscribe to broadcast test events
    public async Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeToBroadCastTestEventsAsync(string publisherType, string? customTopic = null)
    {
        Logger.LogInformation("Subscribing to broadcast events from {PublisherType}", publisherType);
        _handle = await SubscribeBroadCastEventAsync<BroadCastTestEvent>(
            publisherType,
            HandleBroadCastTestEventAsync,
            customTopic);
        return _handle;
    }

    // Unsubscribe from broadcast events
    public async Task UnsubscribeFromBroadCastTestEventsAsync(string publisherType, string? customTopic = null)
    {
        if (_handle != null)
        {
            Logger.LogInformation("Unsubscribing from broadcast events from {PublisherType}", publisherType);
            await UnSubscribeBroadCastAsync<BroadCastTestEvent>(publisherType, _handle, customTopic);
            _handle = null;
        }
    }

    // Handle received broadcast events
    private async Task HandleBroadCastTestEventAsync(BroadCastTestEvent eventData)
    {
        Logger.LogInformation("Received broadcast event: {Message}", eventData.Message);
        
        // Create and raise state log event
        var addMessageEvent = new AddMessageStateLogEvent
        {
            Message = eventData.Message
        };
        RaiseEvent(addMessageEvent);
        await ConfirmEvents();
    }

    // Get the list of received messages
    public Task<List<string>> GetReceivedMessagesAsync()
    {
        return Task.FromResult(State.ReceivedMessages ?? new List<string>());
    }

    // State transition method - handle message addition
    protected override void GAgentTransitionState(BroadCastTestGAgentState state, StateLogEventBase<BroadCastTestStateLogEvent> @event)
    {
        if (@event is AddMessageStateLogEvent addMessageEvent)
        {
            if (state.ReceivedMessages == null)
            {
                state.ReceivedMessages = new List<string>();
            }
            state.ReceivedMessages.Add(addMessageEvent.Message);
        }
        
        // Call base class method to handle subscription-related state
        base.GAgentTransitionState(state, @event);
    }

    // Get subscription information
    public Task<Dictionary<string, Guid>> GetSubscriptionsAsync()
    {
        return Task.FromResult(State.Subscription);
    }
} 