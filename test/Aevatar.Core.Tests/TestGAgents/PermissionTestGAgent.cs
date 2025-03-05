using Aevatar.Core.Abstractions;
using Aevatar.PermissionManagement;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class PermissionGAgentState : StateBase
{

}

[GenerateSerializer]
public class PermissionStateLogEvent : StateLogEventBase<PermissionStateLogEvent>
{

}

public interface IPermissionGAgent : IGAgent
{
    Task DoSomething1Async();
    Task DoSomething2Async();
    Task DoSomething3Async();
}

[GAgent]
public class PermissionGAgent : GAgentBase<PermissionGAgentState, PermissionStateLogEvent>, IPermissionGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing permissions.");
    }

    [Permission("AbpIdentity.Roles.Create", displayName: "Only for testing.")]
    public Task DoSomething1Async()
    {
        return Task.CompletedTask;
    }
    
    [Permission("DoSomething2", groupName: "DefaultGroup")]
    public Task DoSomething2Async()
    {
        return Task.CompletedTask;
    }

    [Permission("DoSomething3", groupName: "AnotherGroup")]
    public Task DoSomething3Async()
    {
        return Task.CompletedTask;
    }
}