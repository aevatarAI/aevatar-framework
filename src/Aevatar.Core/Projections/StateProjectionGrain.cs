using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Streams;

namespace Aevatar.Core.Projections;

public class StateProjectionGrain : Grain, IProjectionGrain
{
    private readonly AevatarOptions AevatarOptions;
    private IStreamProvider StreamProvider => this.GetStreamProvider(AevatarCoreConstants.StreamProvider);
    private ILogger<StateProjectionGrain> _logger;

    private string StateTypeName => this.GetPrimaryKeyString();
    
    public StateProjectionGrain(ILogger<StateProjectionGrain> logger, IOptionsSnapshot<AevatarOptions> aevatarOptions)
    {
        _logger = logger;
        AevatarOptions = aevatarOptions.Value;
    }
    
    public Task ActivateAsync()
    {
        _logger.LogInformation("Someone activated StateProjectionGrain<{TState}>", StateTypeName);
        return Task.CompletedTask;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await InitializeOrResumeStateProjectionStreamAsync();
    }
    
    private async Task InitializeOrResumeStateProjectionStreamAsync()
    {
        _logger.LogInformation("Initializing or resuming state projection stream for {TState}", StateTypeName);
        var projectionStream = GetStateProjectionStream();
        var handles = await projectionStream.GetAllSubscriptionHandles();
        var projectors = ServiceProvider.GetRequiredService<IEnumerable<IStateProjector>>();
        var asyncObserver = StateProjectionAsyncObserver.Create(projectors, ServiceProvider);
        if (handles.Count > 0)
        {
            _logger.LogInformation("Resuming state projection stream for {TState} with handle count of {Count}", StateTypeName, handles.Count);
            foreach (var handle in handles)
            {
                await handle.ResumeAsync(asyncObserver);
            }
        }
        else
        {
            _logger.LogInformation("Subscribing for the first time to state projection stream for {TState}", StateTypeName);
            await projectionStream.SubscribeAsync(asyncObserver);
        }
        _logger.LogInformation("State projection stream for {TState} is ready", StateTypeName);
    }
    
    private IAsyncStream<StateWrapper<StateBase>> GetStateProjectionStream()
    {
        var streamId = StreamId.Create(AevatarOptions.StreamNamespace, StateTypeName);
        return StreamProvider.GetStream<StateWrapper<StateBase>>(streamId);
    }
}