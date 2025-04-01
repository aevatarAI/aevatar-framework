using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Exceptions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
{
    private Guid? _correlationId;
    private GrainId GrainId => this.GetGrainId();

    protected async Task PublishAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        try
        {
            await _coordinator!.UpwardsEventAsync(eventWrapper);
            await _coordinator!.DownwardsEventAsync(eventWrapper);
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to publish response event {EventWrapper}", GrainId.ToString(),
                eventWrapper);
            throw new EventPublishingException($"{GrainId.ToString()} failed to publish response event", ex);
        }
    }

    protected async Task<Guid> PublishAsync<T>(T @event) where T : EventBase
    {
        _correlationId ??= Guid.NewGuid();
        @event.CorrelationId = _correlationId;
        @event.PublisherGrainId = GrainId;
        Logger.LogInformation("Published event {@Event}, {CorrelationId}", @event, _correlationId);

        var eventId = Guid.NewGuid();
        try
        {
            var eventWrapper = new EventWrapper<T>(@event, eventId, GrainId);
            await _coordinator!.PublishEventAsync(eventWrapper);
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to publish event {EventJson}", GrainId.ToString(),
                JsonConvert.SerializeObject(@event));
            throw new EventPublishingException($"{GrainId.ToString()} failed to publish event", ex);
        }

        return eventId;
    }
}