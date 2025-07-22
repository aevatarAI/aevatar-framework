using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.PermissionManagement;

[GenerateSerializer]
public class EventBaseTypeTestGAgentState : NaiveTestGAgentState;

[GenerateSerializer]
public class EventBaseTypeTestStateLogEvent : StateLogEventBase<EventBaseTypeTestStateLogEvent>;

[GenerateSerializer]
public class TestPermissionEvent : PermissionEventBase;

[GAgent]
public class EventBaseTypeTestGAgent : GAgentBase<EventBaseTypeTestGAgentState, EventBaseTypeTestStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing event base types.");
    }

    public async Task HandleEventAsync(TestPermissionEvent @event)
    {
        State.Content.Add("test");
    }
}