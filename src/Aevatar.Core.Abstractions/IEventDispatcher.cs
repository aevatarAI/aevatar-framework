using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Streams;

namespace Aevatar.Core.Abstractions;

public interface IEventDispatcher
{
    Task PublishAsync(string grainId, StateBase state);
    Task PublishAsync(GrainId grainId, StateBase state);
    Task PublishAsync(Guid eventId, GrainId grainId, StateLogEventBase stateLogEvent);
    Task PublishAsync(Guid eventId, string grainId, StateLogEventBase stateLogEvent);

    Task PublishAsync<TState>(GrainId grainId, TState state) where TState : StateBase;

    Task PublishAsync<TStateLogEvent>(Guid eventId, GrainId grainId, TStateLogEvent stateLogEvent)
        where TStateLogEvent : StateLogEventBase;
}

public class DefaultEventDispatcher : IEventDispatcher
{
    private readonly IStreamProvider _streamProvider;
    private readonly AevatarOptions _aevatarOptions;

    public DefaultEventDispatcher(IClusterClient clusterClient)
    {
        _streamProvider = clusterClient.GetStreamProvider(AevatarCoreConstants.StreamProvider);
        _aevatarOptions = clusterClient.ServiceProvider.GetRequiredService<IOptions<AevatarOptions>>().Value;
    }
    
    public Task PublishAsync(string grainId, StateBase state)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync(GrainId grainId, StateBase state)
    {
        return Task.CompletedTask;
    }

    public async Task PublishAsync<TState>(GrainId grainId, TState state) where TState : StateBase
    {
        var streamId = StreamId.Create(_aevatarOptions.StreamNamespace, typeof(TState).FullName!);
        var stream = _streamProvider.GetStream<TState>(streamId);
        await stream.OnNextAsync(state);
    }

    public async Task PublishAsync<TStateLogEvent>(Guid eventId, GrainId grainId, TStateLogEvent stateLogEvent) where TStateLogEvent : StateLogEventBase
    {
        var streamId = StreamId.Create(_aevatarOptions.StreamNamespace, typeof(TStateLogEvent).FullName!);
        var stream = _streamProvider.GetStream<TStateLogEvent>(streamId);
        await stream.OnNextAsync(stateLogEvent);
    }

    public Task PublishAsync(Guid eventId, GrainId grainId, StateLogEventBase stateLogEvent)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync(Guid eventId, string grainId, StateLogEventBase stateLogEvent)
    {
        return Task.CompletedTask;
    }
}