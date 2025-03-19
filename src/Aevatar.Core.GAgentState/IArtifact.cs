using Aevatar.Core.Interface;

namespace Aevatar.Core.GAgentState;

public interface IArtifact<in TState, TStateLogEvent>
    where TState : IGAgentState
    where TStateLogEvent : IGAgentEventBase<TStateLogEvent>
{
    void TransitionState(IGAgentState state, IGAgentEventBase<TStateLogEvent> stateLogEvent);
    string GetDescription();
}

public interface IArtifactGAgent;
