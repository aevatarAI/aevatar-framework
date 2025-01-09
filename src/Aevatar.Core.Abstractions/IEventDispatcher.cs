namespace Aevatar.Core.Abstractions;

public interface IEventDispatcher
{
    Task PublishAsync(StateBase state, string id);
    Task PublishAsync(Guid eventId, GrainId grainId, StateLogEventBase eventBase);
}