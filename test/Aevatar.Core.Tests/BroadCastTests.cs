using Aevatar.Core.Tests.TestGAgents;
using Shouldly;

namespace Aevatar.Core.Tests;

[Trait("Category", "BVT")]
public class BroadCastTests : GAgentTestKitBase
{
    // Constants for topics
    private const string DefaultTopic = "DefaultTestTopic";
    private const string CustomTopic = "CustomTestTopic";

    [Fact(DisplayName = "Agent can broadcast and receive events with default topic")]
    public async Task BroadCastEventDefaultTopicTest()
    {
        // Arrange
        var publisherAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        
        AddProbesByGrainId(publisherAgent, subscriberAgent);
        
        // Act - Subscribe with explicit default topic
        var publisherType = publisherAgent.GetType().FullName!;
        await subscriberAgent.SubscribeToBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Act - Publish with explicit default topic
        string testMessage = "Test broadcast message";
        await publisherAgent.PublishBroadCastTestEventAsync(testMessage, DefaultTopic);
        
        // Assert - Check if message was received
        var receivedMessages = await subscriberAgent.GetReceivedMessagesAsync();
        receivedMessages.ShouldNotBeNull();
        receivedMessages.Count.ShouldBe(1);
        receivedMessages.ShouldContain(testMessage);
        
        // Act - Publish another message with explicit default topic
        string secondMessage = "Second broadcast message";
        await publisherAgent.PublishBroadCastTestEventAsync(secondMessage, DefaultTopic);
        
        // Assert - Check if second message was also received
        receivedMessages = await subscriberAgent.GetReceivedMessagesAsync();
        receivedMessages.Count.ShouldBe(2);
        receivedMessages.ShouldContain(secondMessage);
    }
    
    [Fact(DisplayName = "Agent can broadcast and receive events with custom topic")]
    public async Task BroadCastEventCustomTopicTest()
    {
        // Arrange
        var publisherAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberCustomAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberDefaultAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        
        AddProbesByGrainId(publisherAgent, subscriberCustomAgent, subscriberDefaultAgent);
        
        // Act - Subscribe with custom topic
        var publisherType = publisherAgent.GetType().FullName!;
        await subscriberCustomAgent.SubscribeToBroadCastTestEventsAsync(publisherType, CustomTopic);
        
        // Act - Subscribe with default topic
        await subscriberDefaultAgent.SubscribeToBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Act - Publish with custom topic
        string testMessage = "Test broadcast message with custom topic";
        await publisherAgent.PublishBroadCastTestEventAsync(testMessage, CustomTopic);
        
        // Assert - Check if custom topic message was received by custom topic subscriber
        var receivedCustomMessages = await subscriberCustomAgent.GetReceivedMessagesAsync();
        receivedCustomMessages.ShouldNotBeNull();
        receivedCustomMessages.Count.ShouldBe(1);
        receivedCustomMessages.ShouldContain(testMessage);
        
        // Assert - Check that default topic subscriber did not receive custom topic message
        var receivedDefaultMessages = await subscriberDefaultAgent.GetReceivedMessagesAsync();
        receivedDefaultMessages.ShouldBeEmpty();
        
        // Act - Publish to default topic
        string defaultTopicMessage = "Default topic message";
        await publisherAgent.PublishBroadCastTestEventAsync(defaultTopicMessage, DefaultTopic);
        
        // Assert - Check that default topic message was received by default topic subscriber
        receivedDefaultMessages = await subscriberDefaultAgent.GetReceivedMessagesAsync();
        receivedDefaultMessages.ShouldNotBeNull();
        receivedDefaultMessages.Count.ShouldBe(1);
        receivedDefaultMessages.ShouldContain(defaultTopicMessage);
        
        // Assert - Check that custom topic subscriber did not receive default topic message
        receivedCustomMessages = await subscriberCustomAgent.GetReceivedMessagesAsync();
        receivedCustomMessages.Count.ShouldBe(1); // Still only one message
        receivedCustomMessages.ShouldNotContain(defaultTopicMessage);
    }
    
    [Fact(DisplayName = "Agent can unsubscribe from broadcast events")]
    public async Task UnsubscribeFromBroadCastEventTest()
    {
        // Arrange
        var publisherAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        
        AddProbesByGrainId(publisherAgent, subscriberAgent);
        
        // Act - Subscribe with explicit topic
        var publisherType = publisherAgent.GetType().FullName!;
        await subscriberAgent.SubscribeToBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Act - Publish first message with explicit topic
        string firstMessage = "First broadcast message";
        await publisherAgent.PublishBroadCastTestEventAsync(firstMessage, DefaultTopic);
        
        // Assert - Check if first message was received
        var receivedMessages = await subscriberAgent.GetReceivedMessagesAsync();
        receivedMessages.ShouldNotBeNull();
        receivedMessages.Count.ShouldBe(1);
        receivedMessages.ShouldContain(firstMessage);
        
        // Act - Unsubscribe with explicit topic
        await subscriberAgent.UnsubscribeFromBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Act - Publish second message with explicit topic
        string secondMessage = "Second broadcast message";
        await publisherAgent.PublishBroadCastTestEventAsync(secondMessage, DefaultTopic);
        
        // Assert - Check that second message was not received
        receivedMessages = await subscriberAgent.GetReceivedMessagesAsync();
        receivedMessages.Count.ShouldBe(1); // Still only one message
        receivedMessages.ShouldNotContain(secondMessage);
    }
    
    [Fact(DisplayName = "Multiple agents can subscribe to broadcast events")]
    public async Task MultipleSubscribersTest()
    {
        // Arrange
        var publisherAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberAgent1 = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberAgent2 = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        
        AddProbesByGrainId(publisherAgent, subscriberAgent1, subscriberAgent2);
        
        // Act - Subscribe both agents with explicit topic
        var publisherType = publisherAgent.GetType().FullName!;
        await subscriberAgent1.SubscribeToBroadCastTestEventsAsync(publisherType, DefaultTopic);
        await subscriberAgent2.SubscribeToBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Act - Publish message with explicit topic
        string testMessage = "Test broadcast message for multiple subscribers";
        await publisherAgent.PublishBroadCastTestEventAsync(testMessage, DefaultTopic);
        
        // Assert - Check if both subscribers received the message
        var receivedMessages1 = await subscriberAgent1.GetReceivedMessagesAsync();
        receivedMessages1.ShouldNotBeNull();
        receivedMessages1.Count.ShouldBe(1);
        receivedMessages1.ShouldContain(testMessage);
        
        var receivedMessages2 = await subscriberAgent2.GetReceivedMessagesAsync();
        receivedMessages2.ShouldNotBeNull();
        receivedMessages2.Count.ShouldBe(1);
        receivedMessages2.ShouldContain(testMessage);
        
        // Act - Unsubscribe one agent with explicit topic
        await subscriberAgent1.UnsubscribeFromBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Act - Publish second message with explicit topic
        string secondMessage = "Second broadcast message";
        await publisherAgent.PublishBroadCastTestEventAsync(secondMessage, DefaultTopic);
        
        // Assert - Check that only subscriberAgent2 received the second message
        receivedMessages1 = await subscriberAgent1.GetReceivedMessagesAsync();
        receivedMessages1.Count.ShouldBe(1); // Still only one message
        receivedMessages1.ShouldNotContain(secondMessage);
        
        receivedMessages2 = await subscriberAgent2.GetReceivedMessagesAsync();
        receivedMessages2.Count.ShouldBe(2); // Now two messages
        receivedMessages2.ShouldContain(secondMessage);
    }
    
    [Fact(DisplayName = "Subscription information is properly stored in state")]
    public async Task SubscriptionInfoStoredInStateTest()
    {
        // Arrange
        var publisherAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        var subscriberAgent = await Silo.CreateGrainAsync<BroadCastTestGAgent>(Guid.NewGuid());
        
        AddProbesByGrainId(publisherAgent, subscriberAgent);
        
        // Act - Subscribe with explicit topic
        var publisherType = publisherAgent.GetType().FullName!;
        var handle = await subscriberAgent.SubscribeToBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Assert - Check subscription info
        var subscriptions = await subscriberAgent.GetSubscriptionsAsync();
        subscriptions.ShouldNotBeNull();
        subscriptions.Count.ShouldBe(1);
        
        // The key should contain the publisherType and BroadCastTestEvent
        var key = subscriptions.Keys.First();
        key.ShouldContain(publisherType);
        key.ShouldContain(nameof(BroadCastTestEvent));
        
        // The value should be the handle ID
        var value = subscriptions.Values.First();
        value.ShouldBe(handle.HandleId);
        
        // Act - Unsubscribe with explicit topic
        await subscriberAgent.UnsubscribeFromBroadCastTestEventsAsync(publisherType, DefaultTopic);
        
        // Assert - Check subscription info after unsubscribe
        subscriptions = await subscriberAgent.GetSubscriptionsAsync();
        subscriptions.ShouldNotBeNull();
        subscriptions.Count.ShouldBe(0);
    }
} 