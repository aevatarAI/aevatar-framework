using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestArtifacts;

[GenerateSerializer]
public class MyArtifactGAgentState : StateBase;

[GenerateSerializer]
public class MyArtifactStateLogEvent : StateLogEventBase<MyArtifactStateLogEvent>;

[GenerateSerializer]
public class MyArtifactEvent : EventBase
{
    [Id(0)] public Dictionary<string, object> Content { get; set; }
}

public interface IMyArtifact : IArtifact<MyArtifactGAgentState, MyArtifactStateLogEvent>
{
    string TestMethod();
}

[GenerateSerializer]
public class MyArtifact : IMyArtifact
{
    public string GetDescription() => "MyArtifact Description, this is for testing.";

    [EventHandler]
    public async Task HandleEventAsync(MyArtifactEvent eventData)
    {
        await Task.Yield();
    }

    public string TestMethod()
    {
        return "Test";
    }

    public void TransitionState(MyArtifactGAgentState state, StateLogEventBase<MyArtifactStateLogEvent> stateLogEvent)
    {
        /* custom logic */
    }
}