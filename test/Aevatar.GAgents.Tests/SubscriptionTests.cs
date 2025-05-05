using System.Diagnostics;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Shouldly;
using Xunit.Abstractions;

namespace Aevatar.GAgents.Tests;

public sealed class SubscriptionTests : AevatarGAgentsTestBase
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IGAgentFactory _gAgentFactory;

    public SubscriptionTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public async Task ManyChildrenTest(int count)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var children = new List<IChildTestGAgent>();
        for (var i = 0; i < count; i++)
        {
            var child = await _gAgentFactory.GetGAgentAsync<IChildTestGAgent>();
            children.Add(child);
        }

        var groupGAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<GroupGAgentState>>(Guid.NewGuid());
        foreach (var child in children)
        {
            await groupGAgent.RegisterAsync(child);
        }

        var publishingGAgent = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>();
        await publishingGAgent.RegisterAsync(groupGAgent);
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "test"
        });

        await Task.Delay(10000);

        foreach (var child in children.AsParallel())
        {
            var state = await child.GetStateAsync();
            state.Content.Count.ShouldBe(1);
            state.Content[0].ShouldBe("test");
        }

        stopWatch.Stop();
        _outputHelper.WriteLine($"Time: {stopWatch.ElapsedMilliseconds}ms");
    }
}