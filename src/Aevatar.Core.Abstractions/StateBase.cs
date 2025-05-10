using Aevatar.Agent.Abstractions;

namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public abstract class StateBase : IGAgentState
{
    [Id(0)] public List<GrainId> Children { get; set; } = [];
    [Id(1)] public GrainId? Parent { get; set; }
    [Id(2)] public string? GAgentCreator { get; set; }

    public void Apply(IGAgentEventBase @stateLogEvent)
    {
        // Just to avoid exception on GAgentBase.TransitionState.
    }
}