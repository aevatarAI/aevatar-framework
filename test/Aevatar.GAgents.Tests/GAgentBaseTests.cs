using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.Core.Tests.TestStates;
using Shouldly;

namespace Aevatar.GAgents.Tests;

public class GAgentBaseTests : AevatarGAgentsTestBase
{
    protected readonly IGrainFactory _grainFactory;

    public GAgentBaseTests()
    {
        _grainFactory = GetRequiredService<IGrainFactory>();
    }

    [Fact]
    public async Task ComplicatedEventHandleTest()
    {
        var guid = Guid.NewGuid();
        // Arrange.
        var marketingLeader = _grainFactory.GetGrain<IMarketingLeaderTestGAgent>(guid);
        var developingLeader = _grainFactory.GetGrain<IDevelopingLeaderTestGAgent>(guid);

        var developer1 = _grainFactory.GetGrain<IDeveloperTestGAgent>(guid);
        var developer2 = _grainFactory.GetGrain<IDeveloperTestGAgent>(Guid.NewGuid());
        var developer3 = _grainFactory.GetGrain<IDeveloperTestGAgent>(Guid.NewGuid());
        await developingLeader.RegisterAsync(developer1);
        await developingLeader.RegisterAsync(developer2);
        await developingLeader.RegisterAsync(developer3);

        var investor1 = _grainFactory.GetGrain<IStateGAgent<InvestorTestGAgentState>>(guid);
        var investor2 = _grainFactory.GetGrain<IStateGAgent<InvestorTestGAgentState>>(Guid.NewGuid());
        await marketingLeader.RegisterAsync(investor1);
        await marketingLeader.RegisterAsync(investor2);

        var groupGAgent = _grainFactory.GetGrain<IStateGAgent<GroupGAgentState>>(guid);
        await groupGAgent.RegisterAsync(marketingLeader);
        await groupGAgent.RegisterAsync(developingLeader);
        var publishingGAgent = _grainFactory.GetGrain<IPublishingGAgent>(guid);
        await publishingGAgent.RegisterAsync(groupGAgent);

        // Act.
        await publishingGAgent.PublishEventAsync(new NewDemandTestEvent
        {
            Description = "New demand from customer."
        });

        await TestHelper.WaitUntilAsync(_ => CheckState(investor1), TimeSpan.FromSeconds(20));

        var groupState = await groupGAgent.GetStateAsync();
        groupState.RegisteredGAgents.ShouldBe(2);

        var investorState = await investor1.GetStateAsync();
        investorState.Content.Count.ShouldBe(2);
    }
    
    [Fact(DisplayName = "Call unregister immidiately after publishing.")]
    public async Task RegisterAndUnregisterTest()
    {
        var publishingGAgent = _grainFactory.GetGrain<IPublishingGAgent>(Guid.NewGuid());
        var testGAgent = _grainFactory.GetGrain<IStateGAgent<EventHandlerTestGAgentState>>(Guid.NewGuid());
        await publishingGAgent.RegisterAsync(testGAgent);
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Test"
        });
        await publishingGAgent.UnregisterAsync(testGAgent);

        await TestHelper.WaitUntilAsync(_ => CheckState(testGAgent), TimeSpan.FromSeconds(20));

        var state = await testGAgent.GetStateAsync();
        state.Content.Count.ShouldBe(3);
    }
    
    private async Task<bool> CheckState(IStateGAgent<InvestorTestGAgentState> investorGAgent)
    {
        var state = await investorGAgent.GetStateAsync();
        return !state.Content.IsNullOrEmpty() && state.Content.Count == 2;
    }
    
    private async Task<bool> CheckState(IStateGAgent<EventHandlerTestGAgentState> evnetHandlerGAgent)
    {
        var state = await evnetHandlerGAgent.GetStateAsync();
        return !state.Content.IsNullOrEmpty() && state.Content.Count == 3;
    }
}