using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Shouldly;

namespace Aevatar.Core.Tests;

[Trait("Category", "BVT")]
public class GroupingTests : GAgentTestKitBase
{
    [Fact(DisplayName = "GroupGAgent should be initialized correctly.")]
    public async Task InitGroupGAgentTest()
    {
        // Arrange & Act.
        var groupGAgent = await Silo.CreateGrainAsync<GroupGAgent>(Guid.NewGuid());

        // Assert: Subscribers should be empty because no member is registered.
        var subscribers = await groupGAgent.GetChildrenAsync();
        subscribers.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GroupGAgent's states should be saved correctly after register.")]
    public async Task RegisterTest()
    {
        // Arrange & Act.
        var naiveTestGAgent = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(naiveTestGAgent);

        // Assert: Check group's states from GrainStorage.
        var subscribers = await groupGAgent.GetChildrenAsync();
        subscribers.Count.ShouldBe(1);
        subscribers.First().ShouldBe(naiveTestGAgent.GetGrainId());
    }

    [Fact(DisplayName = "GroupGAgent's states should be saved correctly after unregister.")]
    public async Task UnregisterTest()
    {
        // Arrange.
        var naiveTestGAgent = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(naiveTestGAgent);

        // Act.
        await groupGAgent.UnregisterAsync(naiveTestGAgent);

        // Assert: Check group's states from GrainStorage.
        var subscribers = await groupGAgent.GetChildrenAsync();
        subscribers.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Multiple gAgents should be registered correctly to one group.")]
    public async Task OneGroupRegisterMultipleGAgentsTest()
    {
        // Arrange & Act.
        var naiveTestGAgent1 = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var naiveTestGAgent2 = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var naiveTestGAgent3 = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var groupGAgent = await CreateGroupGAgentAsync(naiveTestGAgent1, naiveTestGAgent2, naiveTestGAgent3);

        // Assert: Check group's states from GrainStorage.
        var subscribers = await groupGAgent.GetChildrenAsync();
        subscribers.Count.ShouldBe(3);
    }

    [Fact(DisplayName = "One gAgent should be registered correctly to multiple group.")]
    public async Task MultipleGroupRegisterOneGAgentTest()
    {
        // Arrange & Act.
        var naiveTestGAgent = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var groupGAgent1 = await CreateGroupGAgentAsync(naiveTestGAgent);
        var groupGAgent2 = await CreateGroupGAgentAsync(naiveTestGAgent);
        var groupGAgent3 = await CreateGroupGAgentAsync(naiveTestGAgent);

        // Assert: Check each group's states from GrainStorage.
        foreach (var groupGAgent in new List<GroupGAgent> { groupGAgent1, groupGAgent2, groupGAgent3 })
        {
            var subscribers = await groupGAgent.GetChildrenAsync();
            subscribers.Count.ShouldBe(1);
            subscribers.First().ShouldBe(naiveTestGAgent.GetGrainId());
        }
    }

    [Fact(DisplayName = "One gAgent should be unregistered correctly from multiple group.")]
    public async Task MultipleGroupRegisterAndUnregisterOneGAgentTest()
    {
        // Arrange.
        var naiveTestGAgent = await Silo.CreateGrainAsync<NaiveTestGAgent>(Guid.NewGuid());
        var groupGAgent1 = await CreateGroupGAgentAsync(naiveTestGAgent);
        var groupGAgent2 = await CreateGroupGAgentAsync(naiveTestGAgent);
        var groupGAgent3 = await CreateGroupGAgentAsync(naiveTestGAgent);

        // Act.
        await groupGAgent1.UnregisterAsync(naiveTestGAgent);

        // Assert: Check groupGAgent1's states from GrainStorage.
        {
            var subscribers = await groupGAgent1.GetChildrenAsync();
            subscribers.Count.ShouldBe(0);
        }

        // Act.
        await groupGAgent2.UnregisterAsync(naiveTestGAgent);

        // Assert: Check groupGAgent2's states from GrainStorage.
        {
            var subscribers = await groupGAgent2.GetChildrenAsync();
            subscribers.Count.ShouldBe(0);
        }

        // Act.
        await groupGAgent3.UnregisterAsync(naiveTestGAgent);

        // Assert: Check groupGAgent3's states from GrainStorage.
        {
            var subscribers = await groupGAgent3.GetChildrenAsync();
            subscribers.Count.ShouldBe(0);
        }
    }

    /// <summary>
    /// Structure:
    /// PublishingGAgent -> GroupGAgent -> MarketingLeaderTestGAgent -> InvestorTestGAgent
    /// PublishingGAgent -> GroupGAgent -> DevelopingLeaderTestGAgent -> DeveloperTestGAgent
    ///
    /// 1.
    /// PublishingGAgent: Publish NewDemand
    ///
    /// 2.1
    /// MarketingLeaderTestGAgent: Receive NewDemand, Publish WorkingOnTestEvent
    /// InvestorTestGAgent: Receive WorkingOnTestEvent, Publish InvestorFeedbackTestEvent
    /// MarketingLeaderTestGAgent: Receive InvestorFeedbackTestEvent, add to context
    ///
    /// 2.2
    /// DevelopingLeaderTestGAgent: Receive NewDemand, Publish DevelopTaskTestEvent
    /// DeveloperTestGAgent: Receive DevelopTaskTestEvent, Publish NewFeatureCompletedTestEvent
    ///     (These NewFeatureCompletedTestEvents from DeveloperTestGAgent will only be handled by DevelopingLeaderTestGAgent)
    /// DevelopingLeaderTestGAgent: Receive 3 * NewFeatureCompletedTestEvent, Publish NewFeatureCompletedTestEvent
    ///
    /// 3
    /// MarketingLeaderTestGAgent: Receive NewFeatureCompletedTestEvent, Publish WorkingOnTestEvent
    /// InvestorTestGAgent: Receive WorkingOnTestEvent, Publish InvestorFeedbackTestEvent
    ///
    /// Thus:
    /// Response of DeveloperTestGAgent can be finally handled by MarketingLeaderTestGAgent and InvestorTestGAgent.
    /// </summary>
    [Fact(DisplayName = "Event can be handled by level 4 and responses can be forwarded to level 2.")]
    public async Task SubscriptionTest()
    {
        // Arrange.
        var marketingLeader = await Silo.CreateGrainAsync<MarketingLeaderTestGAgent>(Guid.NewGuid());
        var developingLeader = await Silo.CreateGrainAsync<DevelopingLeaderTestGAgent>(Guid.NewGuid());

        var developer1 = await Silo.CreateGrainAsync<DeveloperTestGAgent>(Guid.NewGuid());
        var developer2 = await Silo.CreateGrainAsync<DeveloperTestGAgent>(Guid.NewGuid());
        var developer3 = await Silo.CreateGrainAsync<DeveloperTestGAgent>(Guid.NewGuid());
        await developingLeader.RegisterAsync(developer1);
        await developingLeader.RegisterAsync(developer2);
        await developingLeader.RegisterAsync(developer3);

        var investor1 = await Silo.CreateGrainAsync<InvestorTestGAgent>(Guid.NewGuid());
        var investor2 = await Silo.CreateGrainAsync<InvestorTestGAgent>(Guid.NewGuid());
        await marketingLeader.RegisterAsync(investor1);
        await marketingLeader.RegisterAsync(investor2);

        var groupGAgent = await CreateGroupGAgentAsync(marketingLeader, developingLeader);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(publishingGAgent, groupGAgent, marketingLeader, developingLeader, developer1, developer2, developer3, investor1, investor2);

        // Act.
        await publishingGAgent.PublishEventAsync(new NewDemandTestEvent
        {
            Description = "New demand from customer."
        });

        await Task.Delay(1000);

        // Assert: Check state of market leading.
        var marketLeadingState = await marketingLeader.GetStateAsync();
        // 2 from market leader -> investors, 2 from developer -> develop leader -> market leader -> investors
        marketLeadingState.Content.Count.ShouldBe(4);

        // Assert: Check state of investor.
        var investorState = await investor1.GetStateAsync();
        investorState.Content.Count.ShouldBe(2);
        var newLineCount = investorState.Content.Last().Count(c => c == '\n');
        newLineCount.ShouldBe(2);
    }

    [Fact(DisplayName = "Cannot register itself.")]
    public async Task RegisterSelfTest()
    {
        var guid = Guid.NewGuid();
        var gAgent = await Silo.CreateGrainAsync<NaiveTestGAgent>(guid);
        await gAgent.RegisterAsync(gAgent);
        var subscribers = await gAgent.GetChildrenAsync();
        subscribers.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Cannot register dup GrainId.")]
    public async Task RegisterSameGrainTest()
    {
        var guid = Guid.NewGuid();
        var gAgent = await Silo.CreateGrainAsync<NaiveTestGAgent>(guid);
        await gAgent.RegisterAsync(gAgent);
        var groupGAgent = await CreateGroupGAgentAsync(gAgent, gAgent);
        
        var subscribers = await groupGAgent.GetChildrenAsync();
        subscribers.Count.ShouldBe(1);
    }
}