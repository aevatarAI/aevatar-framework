using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Streams;

namespace Aevatar.Core;

public class StateDispatcher : IStateDispatcher
{
    private readonly ILogger<StateDispatcher> _logger;
    private readonly IStreamProvider _streamProvider;
    private readonly AevatarOptions _aevatarOptions;

    public StateDispatcher(IClusterClient clusterClient, ILogger<StateDispatcher> logger)
    {
        _logger = logger;
        _streamProvider = clusterClient.GetStreamProvider(AevatarCoreConstants.StreamProvider);
        _aevatarOptions = clusterClient.ServiceProvider.GetRequiredService<IOptions<AevatarOptions>>().Value;
    }

    public async Task PublishAsync<TState>(GrainId grainId, StateWrapper<TState> stateWrapper) where TState : StateBase
    {
        try
        {
            var index =  GetProjectorIndex(grainId);
            var streamId = StreamId.Create(_aevatarOptions.StateProjectionStreamNamespace, typeof(StateWrapper<TState>).FullName! + index);
            _logger.LogInformation($"Publishing state change for grain {grainId} to stream {streamId}-{index}");
            var stream = _streamProvider.GetStream<StateWrapper<TState>>(streamId);
            await stream.OnNextAsync(stateWrapper);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error projecting state for grain {grainId}: {e.Message}");
            throw;
        }
    }

    public async Task PublishSingleAsync<TState>(GrainId grainId, StateWrapper<TState> stateWrapper) where TState : StateBase
    {
        try
        {
            var streamId = StreamId.Create(_aevatarOptions.StateProjectionStreamNamespace, typeof(StateWrapper<TState>).FullName!);
            _logger.LogInformation($"Publishing state change for grain {grainId} to stream {streamId}");
            var stream = _streamProvider.GetStream<StateWrapper<TState>>(streamId);
            await stream.OnNextAsync(stateWrapper);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error projecting state for grain {grainId}: {e.Message}");
            throw;
        }
    }

    private int GetProjectorIndex(GrainId grainId)
    {
        // Compute the hash code of the GrainId
        var hash = grainId.GetHashCode();

        // Combine the hash with the seed to add randomness
        var combinedHash = hash ^ Random.Shared.Next();

        // Ensure the combined hash is non-negative
        var positiveHash = Math.Abs(combinedHash);

        // Return a value between 0 and DefaultNumOfProjectorPerAgentType - 1
        return positiveHash % AevatarCoreConstants.DefaultNumOfProjectorPerAgentType;
    }
}