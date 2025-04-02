using Aevatar.Agent.Abstractions;

namespace Aevatar.Core.GAgentState;

public interface IArtifact<in TState, TStateLogEvent>
    where TState : IGAgentState
    where TStateLogEvent : IGAgentEventBase<TStateLogEvent>
{
    void TransitionState(IGAgentState state, IGAgentEventBase<TStateLogEvent> stateLogEvent);
    string GetDescription();
}

public interface IArtifactGAgent;
