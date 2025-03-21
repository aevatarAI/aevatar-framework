using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]

public class ChildTestGAgentState : StateBase
{
    [Id(0)] public List<string> Content { get; set; } = [];
}

[GenerateSerializer]
public class ChildTestStateLogEvent : StateLogEventBase<ChildTestStateLogEvent>;

public interface IChildTestGAgent : IStateGAgent<ChildTestGAgentState>;

[GAgent]
public class ChildTestGAgent : GAgentBase<ChildTestGAgentState, ChildTestStateLogEvent>, IChildTestGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing child.");
    }
    
    [EventHandler]
    public Task ExecuteAsync(NaiveTestEvent eventData)
    {
        AddContent(eventData.Greeting);
        return Task.CompletedTask;
    }

    private void AddContent(string content)
    {
        State.Content.Add(content);
    }
}