using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Shouldly;

namespace Aevatar.Core.Tests;

[Trait("Category", "BVT")]
public class PublishingTests : GAgentTestKitBase
{
    [Fact(DisplayName = "Event can be published to group members.")]
    public async Task PublishToEventHandlerTest()
    {
        // Arrange.
        var eventHandlerTestGAgent = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(eventHandlerTestGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(publishingGAgent, groupGAgent, eventHandlerTestGAgent);

        // Act.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert.
        var state = await eventHandlerTestGAgent.GetStateAsync();
        state.Content.Count.ShouldBe(3);
        state.Content.ShouldContain("Hello world");
    }

    /// <summary>
    /// level1 -> level21, level22
    /// level21 -> level31, level32
    /// </summary>
    [Fact(DisplayName = "Event can be published downwards to group members.")]
    public async Task MultiLevelDownwardsTest()
    {
        // Arrange.
        var level3A = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var level3B = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var level2A = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var level2B = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        await level2A.RegisterAsync(level3A);
        await level2A.RegisterAsync(level3B);
        var level1 = await CreateGroupGAgentAsync(level2A, level2B);
        var publishingGAgent = await CreatePublishingGAgentAsync(level1);

        AddProbesByGrainId(publishingGAgent, level1, level2A, level2B, level3A, level3B);

        // Act.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert.
        var state3A = await level3A.GetStateAsync();
        state3A.Content.Count.ShouldBe(6);
        var state3B = await level3B.GetStateAsync();
        state3B.Content.Count.ShouldBe(6);
        var state2A = await level2A.GetStateAsync();
        state2A.Content.Count.ShouldBe(6);
        var state2B = await level2B.GetStateAsync();
        state2B.Content.Count.ShouldBe(4);
    }

    [Fact(DisplayName = "Event can be published upwards.")]
    public async Task MultiLevelUpwardsTest()
    {
        // Arrange.
        var level3A = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var level3B = await Silo.CreateGrainAsync<EventHandlerWithResponseTestGAgent>(Guid.NewGuid());
        var level2A = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var level2B = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        await level2A.RegisterAsync(level3A);
        await level2A.RegisterAsync(level3B);
        var level1 = await CreateGroupGAgentAsync(level2A, level2B);
        var publishingGAgent = await CreatePublishingGAgentAsync(level1);

        AddProbesByGrainId(publishingGAgent, level1, level2A, level2B, level3A, level3B);

        // Act: ResponseTestEvent will cause level32 publish an NaiveTestEvent.
        await publishingGAgent.PublishEventAsync(new ResponseTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert: level31 and level21 should receive the response event, then has 1 + 3 content stored.
        var state3A = await level3A.GetStateAsync();
        state3A.Content.Count.ShouldBe(5);
        var state2A = await level2A.GetStateAsync();
        state2A.Content.Count.ShouldBe(5);

        // Assert: level22 should not receive the response event, then has 1 content stored (due to [AllEventHandler]).
        var state2B = await level2B.GetStateAsync();
        state2B.Content.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "Everything works even if the same Guid is used for different grains.")]
    public async Task RegisterSameGuidTest()
    {
        var guid = Guid.NewGuid();
        // Arrange.
        var level3A = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(guid);
        var level3B = await Silo.CreateGrainAsync<EventHandlerWithResponseTestGAgent>(guid);
        var level2A = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        var level2B = await Silo.CreateGrainAsync<EventHandlerTestGAgent>(Guid.NewGuid());
        await level2A.RegisterAsync(level3A);
        await level2A.RegisterAsync(level3B);
        var level1 = await Silo.CreateGrainAsync<GroupGAgent>(guid);
        await level1.RegisterAsync(level2A);
        await level1.RegisterAsync(level2B);
        var publishingGAgent = await CreatePublishingGAgentAsync(level1);

        AddProbesByGrainId(publishingGAgent, level1, level2A, level2B, level3A, level3B);

        // Act: ResponseTestEvent will cause level32 publish an NaiveTestEvent.
        await publishingGAgent.PublishEventAsync(new ResponseTestEvent
        {
            Greeting = "Hello world"
        });

        // Assert: level31 and level21 should receive the response event, then has 1 + 3 content stored.
        var state3A = await level3A.GetStateAsync();
        state3A.Content.Count.ShouldBe(5);
        var state2A = await level2A.GetStateAsync();
        state2A.Content.Count.ShouldBe(5);

        // Assert: level22 should not receive the response event, then has 1 content stored (due to [AllEventHandler]).
        var state2B = await level2B.GetStateAsync();
        state2B.Content.Count.ShouldBe(2);
    }
}