using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Aevatar.Core;

public class StateProjectionAsyncObserver : IAsyncObserver<StateWrapperBase>
{
    private readonly IEnumerable<IStateProjector> _stateProjectors;
    private readonly ILogger<StateProjectionAsyncObserver> _logger;

    public StateProjectionAsyncObserver(IEnumerable<IStateProjector> stateProjectors,
        ILogger<StateProjectionAsyncObserver> logger)
    {
        _stateProjectors = stateProjectors;
        _logger = logger;
    }

    public static StateProjectionAsyncObserver Create(IEnumerable<IStateProjector> stateProjectors,
        IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<StateProjectionAsyncObserver>();
        return new StateProjectionAsyncObserver(stateProjectors, logger);
    }

    public Task OnNextAsync(StateWrapperBase item, StreamSequenceToken? token = null)
    {
        return Task.WhenAll(_stateProjectors.Select(projector => projector.ProjectAsync(item)));
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger?.LogError(ex, "Error invoking state projector.");
        return Task.CompletedTask;
    }
}