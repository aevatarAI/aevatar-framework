namespace Aevatar.Core.Abstractions;

public interface IArtifactGAgent<TArtifact, TState, TStateLogEvent> : IGAgent
    where TArtifact : IArtifact<TState, TStateLogEvent>
    where TState : StateBase
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
{
    Task<TArtifact> GetArtifactAsync();
}