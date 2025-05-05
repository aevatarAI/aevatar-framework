using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.EventPublish;
using Aevatar.Core.EventPublish;
using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Shouldly;
using Xunit.Abstractions;

namespace Aevatar.GAgents.Tests;

public sealed class GAgentEventPubTests : AevatarGAgentsTestBase
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly IGrainFactory _grainFactory;

    public GAgentEventPubTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
        _grainFactory = GetRequiredService<IGrainFactory>();
    }

    [Fact]
    public async Task RegisterTest()
    {
        var groupGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<GroupGAgentState>>();
        var gAgent = await _gAgentFactory.GetGAgentAsync<IEventHandlerTestGAgent>();
        await groupGAgent.RegisterAsync(gAgent);

        var parent = await gAgent.GetParentAsync();
        parent.ShouldBe(groupGAgent.GetGrainId());

        var children = await groupGAgent.GetChildrenAsync();
        children.Count.ShouldBe(1);
        children.First().ShouldBe(gAgent.GetGrainId());

        var childrenGroupGrain =
            _grainFactory.GetGrain<IEventPubChildrenGroupGrain>(0, groupGAgent.GetGrainId().ToString());
        (await childrenGroupGrain.GetChildrenCountAsync()).ShouldBe(1);
        (await childrenGroupGrain.GetChildrenAsync()).First().ShouldBe(gAgent.GetGrainId());
    }

    [Fact]
    public async Task UnregisterTest()
    {
        var groupGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<GroupGAgentState>>();
        var gAgent1 = await _gAgentFactory.GetGAgentAsync<IEventHandlerTestGAgent>();
        var gAgent2 = await _gAgentFactory.GetGAgentAsync<IEventHandlerTestGAgent>();
        await groupGAgent.RegisterAsync(gAgent1);
        await groupGAgent.RegisterAsync(gAgent2);
        await groupGAgent.UnregisterAsync(gAgent1);

        var parent = await gAgent1.GetParentAsync();
        parent.ShouldBe(default);

        var children = await groupGAgent.GetChildrenAsync();
        children.Count.ShouldBe(1);
        children.First().ShouldBe(gAgent2.GetGrainId());
    }

    [Fact]
    public async Task MultipleLevelTest()
    {
        // Arrange.
        var marketingLeader = await _gAgentFactory.GetGAgentAsync<IMarketingLeaderTestGAgent>();

        var investor1 = await _gAgentFactory.GetGAgentAsync<IInvestorTestGAgent>();
        var investor2 = await _gAgentFactory.GetGAgentAsync<IInvestorTestGAgent>();
        await marketingLeader.RegisterAsync(investor1);
        await marketingLeader.RegisterAsync(investor2);

        var groupGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<GroupGAgentState>>();
        await groupGAgent.RegisterAsync(marketingLeader);
        var publishingGAgent = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>();
        await publishingGAgent.RegisterAsync(groupGAgent);

        // Act.
        await publishingGAgent.PublishEventAsync(new NewDemandTestEvent
        {
            Description = "New demand from customer."
        });

        await TestHelper.WaitUntilAsync(_ => CheckState(investor1), TimeSpan.FromSeconds(20));

        {
            var investorState = await investor1.GetStateAsync();
            investorState.Content.Count.ShouldBe(1);
        }
        {
            var investorState = await investor2.GetStateAsync();
            investorState.Content.Count.ShouldBe(1);
        }
    }

    private async Task<bool> CheckState(IStateGAgent<InvestorTestGAgentState> investor1)
    {
        var state = await investor1.GetStateAsync();
        return !state.Content.IsNullOrEmpty() && state.Content.Count == 1;
    }
}