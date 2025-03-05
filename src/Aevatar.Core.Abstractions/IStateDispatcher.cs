namespace Aevatar.Core.Abstractions;

public interface IStateDispatcher
{
    Task PublishAsync<TState>(GrainId grainId, StateWrapper<TState> stateWrapper) where TState : StateBase;
}