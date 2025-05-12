namespace Aevatar.Core.Abstractions;

public interface IArtifact<in TState, TStateLogEvent>
    where TState : StateBase
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
{
    void TransitionState(TState state, StateLogEventBase<TStateLogEvent> stateLogEvent);
    string GetDescription();
}