using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core.Tests.TestGAgents;

public interface IBroadCastTestGAgent : IBroadCastGAgent
{
    // Publish a broadcast test event
    Task PublishBroadCastTestEventAsync(string message, string? customTopic = null);
    
    // Subscribe to broadcast test events
    Task<StreamSubscriptionHandle<EventWrapperBase>> SubscribeToBroadCastTestEventsAsync(string publisherType, string? customTopic = null);
    
    // Unsubscribe from broadcast events
    Task UnsubscribeFromBroadCastTestEventsAsync(string publisherType, string? customTopic = null);
    
    // Get the list of received messages
    Task<List<string>> GetReceivedMessagesAsync();
    
    // Get subscription information
    Task<Dictionary<string, Guid>> GetSubscriptionsAsync();
} 