using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests;
using Aevatar.Core.Tests.TestGAgents;
using Shouldly;

namespace Aevatar.GAgents.Tests;

public class StateProjectionGAgentBaseTests : AevatarGAgentsTestBase
{
    private readonly IGAgentFactory _gAgentFactory;

    public StateProjectionGAgentBaseTests()
    {
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
    }

    [Fact]
    public async Task CallStateHandlerTest()
    {
        var groupGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<GroupGAgentState>>();
        var testGAgent = await _gAgentFactory.GetGAgentAsync<ITestStateProjectionGAgent>();
        await Task.Delay(2000);
        await groupGAgent.RegisterAsync(testGAgent);
        await TestHelper.WaitUntilAsync(_ => CheckState(groupGAgent), TimeSpan.FromSeconds(20));
        var state = await testGAgent.GetStateAsync();
        state.StateHandlerCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task TokenUsageProjectionTest()
    {
        var aiGAgent = await _gAgentFactory.GetGAgentAsync<ISampleAIGAgent>();
        var tokenUsageProjectionGAgent =
            await _gAgentFactory.GetGAgentAsync<IStateGAgent<TokenUsageProjectionGAgentState>>();
        await aiGAgent.PretendingChatAsync("whatever");
        await TestHelper.WaitUntilAsync(_ => CheckState(aiGAgent), TimeSpan.FromSeconds(20));
        var state = await tokenUsageProjectionGAgent.GetStateAsync();
        state.TotalUsedToken.ShouldBe(2008);
    }

    private async Task<bool> CheckState(IStateGAgent<GroupGAgentState> groupGAgent)
    {
        var state = await groupGAgent.GetStateAsync();
        return state.RegisteredGAgents > 0;
    }
    
    private async Task<bool> CheckState(ISampleAIGAgent aiGAgent)
    {
        var state = await aiGAgent.GetStateAsync();
        return state.LatestTotalUsageToken > 0;
    }
}