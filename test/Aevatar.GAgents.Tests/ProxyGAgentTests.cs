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
        var code = await File.ReadAllBytesAsync("ProxyGAgentPlugins/Aevatar.Plugins.Test.dll");
        var proxyGAgent = await _gAgentFactory.GetGAgentAsync("proxy", initializeDto: new ProxyGAgentInitialization
        {
            EventHandlerCode = code
        });
        var proxyTestGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<ProxyTestGAgentState>>();
        var publishingGAgent = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>();
        await publishingGAgent.RegisterAsync(proxyGAgent);
        await publishingGAgent.RegisterAsync(proxyTestGAgent);
        await publishingGAgent.PublishEventAsync(new PluginTestEvent1());
        await TestHelper.WaitUntilAsync(_ => CheckCount(proxyTestGAgent, 1), TimeSpan.FromSeconds(30));
        
        var state = await proxyTestGAgent.GetStateAsync();
        state.Content.Count.ShouldBe(1);
    }
    
    private async Task<bool> CheckCount(IStateGAgent<ProxyTestGAgentState> gAgent, int expectedCount)
    {
        var state = await gAgent.GetStateAsync();
        return state.Content.Count == expectedCount;
    }
}
