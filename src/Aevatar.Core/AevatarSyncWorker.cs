using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.SyncWorker;
using Microsoft.Extensions.Logging;
using Orleans.Streams;
using Orleans.SyncWork;

namespace Aevatar.Core;

public abstract class AevatarSyncWorker<TRequest, TResponse> : SyncWorker<TRequest, TResponse>,
    IAevatarSyncWorker<TRequest, TResponse>
    where TRequest : EventBase
    where TResponse : EventBase
{
    private readonly ILogger<AevatarSyncWorker<TRequest, TResponse>> _logger;
    private IAsyncStream<EventWrapperBase> _asyncStream;

    public AevatarSyncWorker(ILogger<AevatarSyncWorker<TRequest, TResponse>> logger,
        LimitedConcurrencyLevelTaskScheduler limitedConcurrencyScheduler) : base(
        logger, limitedConcurrencyScheduler)
    {
        _logger = logger;
    }

    protected sealed override async Task<TResponse> PerformWork(TRequest request,
        GrainCancellationToken grainCancellationToken)
    {
        _logger.LogInformation($"Performing long run task for request of type {typeof(TRequest).FullName}: {request}");
        try
        {
            var response = await PerformLongRunTask(request);
            _logger.LogInformation($"Performed long run task for request of type {typeof(TRequest).FullName}, response is {response}");
            var eventWrapper = new EventWrapper<TResponse>(response, Guid.NewGuid(), this.GetGrainId());
            eventWrapper.PublishedTimestampUtc = DateTime.UtcNow;
            await _asyncStream.OnNextAsync(eventWrapper);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error performing long run task: {ex.Message}");
            throw;
        }
    }

    protected abstract Task<TResponse> PerformLongRunTask(TRequest request);

    public Task SetLongRunTaskAsync(IAsyncStream<EventWrapperBase> callbackStream)
    {
        _asyncStream = callbackStream;
        return Task.CompletedTask;
    }
}