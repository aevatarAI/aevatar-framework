using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;
using Amazon.Runtime.Internal.Util;
using Xunit.Abstractions;

namespace Aevatar.GAgents.Tests;

public class EventDispatcherTests : AevatarGAgentsTestBase
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IGAgentFactory _gAgentFactory;
    

    public EventDispatcherTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
        _eventDispatcher = GetRequiredService<IEventDispatcher>();
    }

    [Fact]
    public async Task DefaultEventDispatcherTest()
    {
        var groupGAgent = await _gAgentFactory.GetGAgentAsync<IDeveloperTestGAgent>();
        var grainId = groupGAgent.GetGrainId();
        var state  = new DeveloperTestGAgentState();
        var stateLogEvent = new NaiveTestStateLogEvent()
        {
            Id = Guid.NewGuid()
        };
        await _eventDispatcher.PublishAsync(grainId, state);
        await _eventDispatcher.PublishAsync(grainId.ToString(), state);
        await _eventDispatcher.PublishAsync<DeveloperTestGAgentState>(grainId, state);
        // await _eventDispatcher.PublishAsync(stateLogEvent.Id, grainId, stateLogEvent);
        await _eventDispatcher.PublishAsync(stateLogEvent.Id, grainId.ToString(), stateLogEvent);
        // await _eventDispatcher.PublishAsync<NaiveTestStateLogEvent>(stateLogEvent.Id, grainId, stateLogEvent);
    }
}