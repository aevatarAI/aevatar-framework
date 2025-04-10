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
            var streamId = StreamId.Create(_aevatarOptions.StateProjectionStreamNamespace, typeof(StateWrapper<TState>).FullName!);
            var stream = _streamProvider.GetStream<StateWrapper<TState>>(streamId);
            await stream.OnNextAsync(stateWrapper);
        }
        catch (Exception e)
        {
            _logger.LogError($"Error projecting state for grain {grainId}: {e.Message}");
            throw;
        }
    }
}