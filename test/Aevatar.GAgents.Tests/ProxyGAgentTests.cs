using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.ProxyGAgent;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.Plugins.Test;
using Shouldly;

namespace Aevatar.GAgents.Tests;

public class ProxyGAgentTests : AevatarGAgentsTestBase
{
    private readonly IGAgentFactory _gAgentFactory;

    public ProxyGAgentTests()
    {
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
    }

    [Fact]
    public async Task ProxyGAgentEventHandlerTest()
    {
        // Arrange.
        var code = await File.ReadAllBytesAsync("ProxyGAgentPlugins/Aevatar.Plugins.Test.dll");
        var proxyGAgent = await _gAgentFactory.GetGAgentAsync("proxy", initializeDto: new ProxyGAgentInitialization
        {
            PluginCode = code
        });
        var proxyTestGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<ProxyTestGAgentState>>();
        var publishingGAgent = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>();

        // Act.
        await publishingGAgent.RegisterAsync(proxyGAgent);
        await publishingGAgent.RegisterAsync(proxyTestGAgent);
        await publishingGAgent.PublishEventAsync(new PluginTestEvent());
        await TestHelper.WaitUntilAsync(_ => CheckCount(proxyTestGAgent, 1), TimeSpan.FromSeconds(30));

        // Assert.
        var proxyTestGAgentState = await proxyTestGAgent.GetStateAsync();
        proxyTestGAgentState.Content.Count.ShouldBe(1);
        proxyTestGAgentState.Content[0].ShouldBe("Hello from TestEventHandler");
        var proxyGAgentWithState =
            await _gAgentFactory.GetGAgentAsync<IStateGAgent<ProxyGAgentState>>(proxyGAgent.GetPrimaryKey());
        var proxyGAgentState = await proxyGAgentWithState.GetStateAsync();
        proxyGAgentState.Database.ShouldNotBeNull();
        proxyGAgentState.Database.Count.ShouldBe(1);
        proxyGAgentState.Database["Test"].ToString().ShouldBe("Raised event from TestEventHandler");
    }

    private async Task<bool> CheckCount(IStateGAgent<ProxyTestGAgentState> gAgent, int expectedCount)
    {
        var state = await gAgent.GetStateAsync();
        return state.Content.Count == expectedCount;
    }
}