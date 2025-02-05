namespace Aevatar.Core.Abstractions;

public interface IEventDispatcher
{
    Task PublishAsync(StateBase state, string grainId);
    Task PublishAsync(StateBase state, GrainId grainId);
    Task PublishAsync(Guid eventId, GrainId grainId, StateLogEventBase eventBase);
    Task PublishAsync(Guid eventId, string grainId, StateLogEventBase eventBase);
}

public class DefaultEventDispatcher : IEventDispatcher
{
    public Task PublishAsync(StateBase state, string grainId)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync(StateBase state, GrainId grainId)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync(Guid eventId, GrainId grainId, StateLogEventBase eventBase)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync(Guid eventId, string grainId, StateLogEventBase eventBase)
    {
        return Task.CompletedTask;
    }
}