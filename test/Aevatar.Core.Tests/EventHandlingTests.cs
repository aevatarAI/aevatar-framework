using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.GAgents.Tests;
using Shouldly;

namespace Aevatar.Core.Tests;

[Trait("Category", "BVT")]
public class EventHandlingTests : GAgentTestKitBase
{
    [Fact(DisplayName = "Implementation of GetAllSubscribedEventsAsync works.")]
    public async Task EventHandlerRecognizeTest()
    {
        // Arrange.
        var eventHandlerTestGAgent = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());

        {
            // Act.
            var subscribedEventList = await eventHandlerTestGAgent.GetAllSubscribedEventsAsync();

            // Assert.
            subscribedEventList.ShouldNotBeNull();
            subscribedEventList.Count.ShouldBe(4);
            subscribedEventList.ShouldContain(typeof(NaiveTestEvent));
            subscribedEventList.Count(e => e == typeof(NaiveTestEvent)).ShouldBe(3);
            subscribedEventList.ShouldContain(typeof(EventWrapperBase));
        }

        {
            // Act.
            var subscribedEventList = await eventHandlerTestGAgent.GetAllSubscribedEventsAsync(true);

            // Assert.
            subscribedEventList.ShouldNotBeNull();
            subscribedEventList.Count.ShouldBe(5);
            subscribedEventList.ShouldContain(typeof(NaiveTestEvent));
            subscribedEventList.Count(e => e == typeof(NaiveTestEvent)).ShouldBe(3);
            subscribedEventList.ShouldContain(typeof(EventWrapperBase));
            subscribedEventList.ShouldContain(typeof(RequestAllSubscriptionsEvent));
        }
    }

    [Fact(DisplayName = "Event handler's register and unregister works.")]
    public async Task EventHandlerTest()
    {
        // Arrange.
        var eventHandlerTestGAgent = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(eventHandlerTestGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(eventHandlerTestGAgent, groupGAgent, publishingGAgent);

        // Act of registering.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert.
        {
            var state = await eventHandlerTestGAgent.GetStateAsync();
            state.Content.Count.ShouldBe(3);
        }

        // Act of unregistering.
        await groupGAgent.UnregisterAsync(eventHandlerTestGAgent);
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert.
        {
            var state = await eventHandlerTestGAgent.GetStateAsync();
            state.Content.Count.ShouldBe(3);
        }
    }

    [Fact(DisplayName = "Event handling of event with response works.")]
    public async Task EventWithResponseTest()
    {
        // Arrange.
        var eventHandlerTestGAgent = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var eventHandlerWithResponseTestGAgent =
            await Silo.CreateGrainAsync<EventHandlerWithResponseTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(eventHandlerTestGAgent, eventHandlerWithResponseTestGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(eventHandlerTestGAgent, eventHandlerWithResponseTestGAgent, groupGAgent, publishingGAgent);

        // Act.
        await publishingGAgent.PublishEventAsync(new ResponseTestEvent
        {
            Greeting = "Hello, this is Aevatar."
        });

        // Assert: Events will forward to eventHandlerTestGAgent
        var state = await eventHandlerTestGAgent.GetStateAsync();
        state.Content.Count.ShouldBe(4);
        state.Content.ShouldContain("Hello, this is Aevatar.");
        state.Content.ShouldContain(content => content.Contains(nameof(ResponseTestEvent)));
        state.Content.ShouldContain(content => content.Contains(nameof(NaiveTestEvent)));
    }

    [Fact(DisplayName = "Event handler must be well coded.")]
    public async Task BadEventHandlerTest()
    {
        // Arrange.
        var badEventHandlerTestGAgent = await Silo.CreateGrainAsync<BadEventHandlerTestGAgent>(Guid.NewGuid());

        // Act.
        var subscribedEventList = await badEventHandlerTestGAgent.GetAllSubscribedEventsAsync();

        // Assert.
        subscribedEventList.ShouldNotBeNull();
        subscribedEventList.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Will throw exception if response type is not inherited from EventBase.",
        Skip = "Will change the implementation soon.")]
    public async Task ResponseReturnTypeNotInheritedFromEventBaseTest()
    {
        // Arrange.
        var badEventHandlerTestGAgent = await Silo.CreateGrainAsync<FatalEventHandlerTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(badEventHandlerTestGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        // Act & Assert.
        var exception = Should.Throw<InvalidOperationException>(async () =>
        {
            await publishingGAgent.PublishEventAsync(new ResponseTestEvent
            {
                Greeting = "Expecting an exception."
            });
        });
        exception.Message.ShouldContain("return type needs to be inherited from EventBase.");
    }

    [Fact(DisplayName = "Will throw exception if with-response-type event's event handler has no response type.",
        Skip = "Will change the implementation soon.")]
    public async Task ResponseEventNoResponseTypeTest()
    {
        // Arrange.
        var badEventHandlerTestGAgent = await Silo.CreateGrainAsync<FatalEventHandlerTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(badEventHandlerTestGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        // Act & Assert.
        var exception = Should.Throw<InvalidOperationException>(async () =>
        {
            await publishingGAgent.PublishEventAsync(new AnotherResponseTestEvent
            {
                Greeting = "Expecting an exception."
            });
        });
        exception.Message.ShouldContain("needs to have a return value.");
    }

    [Fact(DisplayName = "Implementation of handling RequestAllSubscriptionsEvent works.")]
    public async Task RequestSubscribedEventListTest()
    {
        // Arrange.
        var eventHandlerTestGAgent = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var eventHandlerWithResponseTestGAgent =
            await Silo.CreateGrainAsync<EventHandlerWithResponseTestGAgent>(Guid.NewGuid());
        var subscribeTestGAgent = await Silo.CreateGrainAsync<SubscribeTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(eventHandlerTestGAgent, eventHandlerWithResponseTestGAgent,
            subscribeTestGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(eventHandlerTestGAgent, eventHandlerWithResponseTestGAgent, subscribeTestGAgent, groupGAgent,
            publishingGAgent);

        // Act.
        await publishingGAgent.PublishEventAsync(new RequestAllSubscriptionsEvent());

        // Assert.
        var state = await subscribeTestGAgent.GetStateAsync();
        state.SubscriptionInfo.Count.ShouldBe(3);
        state.SubscriptionInfo[typeof(EventHandlerTestGAgent)].Count.ShouldBe(4);
        state.SubscriptionInfo[typeof(EventHandlerWithResponseTestGAgent)].Count.ShouldBe(1);
        state.SubscriptionInfo[typeof(SubscribeTestGAgent)].Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Exception from event handlers can be handled.")]
    public async Task ExceptionHandlingTest()
    {
        // Arrange.
        var eventHandlerTestGAgent = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var testGAgent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(eventHandlerTestGAgent, testGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(eventHandlerTestGAgent, testGAgent, groupGAgent, publishingGAgent);

        // Act.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert.
        var state = await testGAgent.GetStateAsync();
        state.ErrorMessages.Count.ShouldBe(1);
    }
}