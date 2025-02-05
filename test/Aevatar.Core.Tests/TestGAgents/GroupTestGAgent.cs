using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestStateLogEvents;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class GroupTestGAgentState : StateBase
{
    [Id(0)] public Guid GroupManagerGuid { get; set; }
    [Id(1)] public int CalledCount { get; set; }
}

[GAgent("groupTest", "test")]
public class GroupTestGAgent: GAgentBase<GroupTestGAgentState, GroupStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("For testing reload group.");
    }
    
    public async Task HandleEventAsync(GroupReloadTestEvent eventData)
    {
        State.GroupManagerGuid = eventData.GroupManagerGuid;
        State.CalledCount++;
    }
}