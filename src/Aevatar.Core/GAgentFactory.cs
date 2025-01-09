using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Streams;

namespace Aevatar.Core;

public interface IGAgentFactory
{
    Task<IGAgent> GetGAgentAsync(GrainId grainId, InitializationDtoEventBase? initializeDto = null);

    Task<IGAgent> GetGAgentAsync(string alias, Guid primaryKey,
        string ns = AevatarGAgentConstants.GAgentDefaultNamespace, InitializationDtoEventBase? initializeDto = null);

    Task<IGAgent> GetGAgentAsync(string alias, string ns = AevatarGAgentConstants.GAgentDefaultNamespace,
        InitializationDtoEventBase? initializeDto = null);

    Task<TGrainInterface> GetGAgentAsync<TGrainInterface>(Guid primaryKey, InitializationDtoEventBase? initializeDto = null)
        where TGrainInterface : IGAgent;

    Task<TGrainInterface> GetGAgentAsync<TGrainInterface>(InitializationDtoEventBase? initializeDto = null)
        where TGrainInterface : IGAgent;
}

public class GAgentFactory : IGAgentFactory
{
    private readonly IClusterClient _clusterClient;
    private readonly IStreamProvider _streamProvider;

    public GAgentFactory(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
        _streamProvider =
            _clusterClient.ServiceProvider.GetRequiredKeyedService<IStreamProvider>(AevatarCoreConstants.StreamProvider);
    }

    public async Task<IGAgent> GetGAgentAsync(GrainId grainId, InitializationDtoEventBase? initializeDto = null)
    {
        var gAgent = _clusterClient.GetGrain<IGAgent>(grainId);
        if (initializeDto != null)
        {
            await InitializeAsync(gAgent, new EventWrapper<EventBase>(initializeDto, Guid.NewGuid(), grainId));
        }

        return gAgent;
    }

    public async Task<IGAgent> GetGAgentAsync(string alias, Guid primaryKey, string ns = "aevatar", InitializationDtoEventBase? initializeDto = null)
    {
        var gAgent = _clusterClient.GetGrain<IGAgent>(GrainId.Create($"{ns}/{alias}", primaryKey.ToString()));
        await gAgent.ActivateAsync();
        if (initializeDto != null)
        {
            await InitializeAsync(gAgent,
                new EventWrapper<EventBase>(initializeDto, Guid.NewGuid(), gAgent.GetGrainId()));
        }

        return gAgent;
    }

    public async Task<IGAgent> GetGAgentAsync(string alias, string ns = "aevatar", InitializationDtoEventBase? initializeDto = null)
    {
        return await GetGAgentAsync(alias, Guid.NewGuid(), ns, initializeDto);
    }

    public async Task<TGrainInterface> GetGAgentAsync<TGrainInterface>(Guid primaryKey, InitializationDtoEventBase? initializeDto = null)
        where TGrainInterface : IGAgent
    {
        var gAgent = _clusterClient.GetGrain<TGrainInterface>(primaryKey);
        if (initializeDto != null)
        {
            await InitializeAsync(gAgent,
                new EventWrapper<EventBase>(initializeDto, Guid.NewGuid(), gAgent.GetGrainId()));
        }

        return gAgent;
    }

    public Task<TGrainInterface> GetGAgentAsync<TGrainInterface>(InitializationDtoEventBase? initializeDto = null)
        where TGrainInterface : IGAgent
    {
        var guid = Guid.NewGuid();
        return GetGAgentAsync<TGrainInterface>(guid, initializeDto);
    }

    private async Task InitializeAsync(IGAgent gAgent, EventWrapperBase eventWrapper)
    {
        var streamId = StreamId.Create(AevatarCoreConstants.StreamNamespace, gAgent.GetGrainId().ToString());
        var stream = _streamProvider.GetStream<EventWrapperBase>(streamId);
        await stream.OnNextAsync(eventWrapper);
    }
}