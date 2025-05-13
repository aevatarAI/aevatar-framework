using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Projections;
using Aevatar.Core.Placement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Streams;

namespace Aevatar.Core.Projections;

[GenerateSerializer]
public class ProjectionState: StateBase
{
    [Id(0)]public int Index { get; set; }
}

[SiloNamePatternPlacement("Projector")]
public class StateProjectionGrain<TState> : Grain, IProjectionGrain<TState>
    where TState : StateBase, new()
{
    private readonly AevatarOptions AevatarOptions;
    private IStreamProvider StreamProvider => this.GetStreamProvider(AevatarCoreConstants.StreamProvider);
    private ILogger<StateProjectionGrain<TState>> _logger;
    private bool _activated = false;

    private readonly IPersistentState<ProjectionState> _projectionState;

    public StateProjectionGrain(ILogger<StateProjectionGrain<TState>> logger,
        IOptionsSnapshot<AevatarOptions> aevatarOptions,
        [PersistentState("ProjectorIndex", "PubSubStore")] IPersistentState<ProjectionState> projectionState)
    {
        _projectionState = projectionState;
        _logger = logger;
        AevatarOptions = aevatarOptions.Value;
    }

    public Task ActivateAsync()
    {
        _logger.LogInformation("Someone activated StateProjectionGrain<{TState}>, id={State}", typeof(TState).Name, _projectionState.State.Index);
        return Task.CompletedTask;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_activated)
        {
            _logger.LogInformation("State projection stream for {TState} already activated.", typeof(TState).Name);
            return;
        }

        _logger.LogDebug("[RequestContext][{0}]Projector Index: {1}", typeof(TState).Name, RequestContext.Get("id"));
        if (RequestContext.Get("id") is int id)
        {
            _projectionState.State.Index = id;
            await _projectionState.WriteStateAsync();
            _logger.LogInformation("State projection grain for {TState} set id to {Id}", typeof(TState).Name, id);
        }
        else
        {
            _logger.LogWarning("RequestContext does not contain a valid 'id' for StateProjectionGrain<{TState}>.", typeof(TState).Name);
        }

        await base.OnActivateAsync(cancellationToken);
        try
        {
            await InitializeOrResumeStateProjectionStreamAsync();
        }
        catch (Exception e)
        {
            _logger.LogError("Error initializing or resuming state projection stream for {TState}: {Error}",
                typeof(TState).Name, e);
            throw;
        }

        _activated = true;
        _logger.LogInformation("State projection stream for {TState} is activated and ready to use on silo {SiloIdentity}.", 
            typeof(TState).Name, this.RuntimeIdentity);
    }

    private async Task InitializeOrResumeStateProjectionStreamAsync()
    {
        try
        {
            _logger.LogInformation("Initializing or resuming state projection stream for {TState}",
                typeof(TState).Name);
            var projectionStream = GetStateProjectionStream();
            var handles = await projectionStream.GetAllSubscriptionHandles();
            var projectors = ServiceProvider.GetRequiredService<IEnumerable<IStateProjector>>();
            var asyncObserver = StateProjectionAsyncObserver.Create(projectors, ServiceProvider);
            if (handles.Count > 0)
            {
                _logger.LogInformation("Resuming state projection stream for {TState} with handle count of {Count}",
                    typeof(TState).Name, handles.Count);
                foreach (var handle in handles)
                {
                    await handle.ResumeAsync(asyncObserver);
                }
            }
            else
            {
                _logger.LogInformation("Subscribing for the first time to state projection stream for {TState}",
                    typeof(TState).Name);
                await projectionStream.SubscribeAsync(asyncObserver);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error initializing or resuming state projection stream for {TState}: {Error}",
                typeof(TState).Name, e);
            throw;
        }

        _logger.LogInformation("State projection stream for {TState} is ready", typeof(TState).Name);
    }

    private IAsyncStream<StateWrapper<TState>> GetStateProjectionStream()
    {
        var streamId = StreamId.Create(AevatarOptions.StateProjectionStreamNamespace, typeof(StateWrapper<TState>).FullName! + _projectionState.State.Index);
        _logger.LogInformation("Getting state projection stream for {TState} with id {Id}", typeof(TState).Name, streamId);
        return StreamProvider.GetStream<StateWrapper<TState>>(streamId);
    }
}