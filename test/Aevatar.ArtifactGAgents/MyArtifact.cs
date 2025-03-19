using Aevatar.Core.GAgentState;
using Aevatar.Core.Interface;
using Aevatar.Core.Abstractions;

namespace Aevatar.ArtifactGAgents;

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

    public void TransitionState(IGAgentState state, IGAgentEventBase<MyArtifactStateLogEvent> stateLogEvent)
    {
        /* custom logic */
    }
}