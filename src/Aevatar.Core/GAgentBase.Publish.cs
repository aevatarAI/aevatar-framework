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
            await SendEventUpwardsAsync(eventWrapper);
            await SendEventDownwardsAsync(eventWrapper);
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
            // Create event wrapper with context propagation
            var eventWrapper = new EventWrapper<T>(@event, eventId, this.GetGrainId());
            eventWrapper.PublishedTimestampUtc = DateTime.UtcNow;
            if (State.Parent == null)
            {
                Logger.LogInformation(
                    "Event is the first time appeared to silo: {@Event}", @event);
                // This event is the first time appeared to silo.
                await SendEventToSelfAsync(eventWrapper);
            }
            else
            {
                Logger.LogInformation(
                    "{GrainId} is publishing event upwards: {EventJson}",
                    GrainId.ToString(), JsonConvert.SerializeObject(@event));
                await SendEventUpwardsAsync(eventWrapper);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to publish event {EventJson}", GrainId.ToString(),
                JsonConvert.SerializeObject(@event));
            throw new EventPublishingException($"{GrainId.ToString()} failed to publish event", ex);
        }

        return eventId;
    }

    private async Task PublishEventUpwardsAsync<T>(T @event, Guid eventId) where T : EventBase
    {
        try
        {
            var eventWrapper = new EventWrapper<T>(@event, eventId, GrainId);
            eventWrapper.PublishedTimestampUtc = DateTime.UtcNow;
            await SendEventUpwardsAsync(eventWrapper);
            Logger.LogDebug("{GrainId} published {Event} to upwards", GrainId.ToString(),
                JsonConvert.SerializeObject(@event));
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to publish event {EventJson} upwards", GrainId.ToString(),
                JsonConvert.SerializeObject(@event));
            throw new EventPublishingException($"{GrainId.ToString()} failed to publish event upwards", ex);
        }
    }

    private async Task SendEventUpwardsAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        if (State.Parent == null)
        {
            return;
        }

        try
        {
            var stream = GetEventBaseStream(State.Parent.Value);
            await stream.OnNextAsync(eventWrapper);
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to send event {EventWrapper} upwards", GrainId.ToString(),
                eventWrapper);
            throw new EventPublishingException($"{GrainId.ToString()} failed to send event upwards", ex);
        }
    }

    private async Task SendEventToSelfAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        Logger.LogInformation(
            $"{GrainId.ToString()} is sending event to self: {JsonConvert.SerializeObject(eventWrapper)}");
        try
        {
            var streamOfThisGAgent = GetEventBaseStream(GrainId);
            await streamOfThisGAgent.OnNextAsync(eventWrapper);
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to send event {EventWrapper} to itself", GrainId.ToString(),
                eventWrapper);
            throw new EventPublishingException($"{GrainId.ToString()} failed to send event to itself", ex);
        }
    }

    private async Task SendEventDownwardsAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        if (State.Children.IsNullOrEmpty())
        {
            return;
        }

        Logger.LogInformation($"{GrainId.ToString()} has {State.Children.Count} children.");

        try
        {
            foreach (var stream in State.Children.Select(GetEventBaseStream))
            {
                await stream.OnNextAsync(eventWrapper);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("{GrainId} failed to send event {EventWrapper} downwards", GrainId.ToString(),
                eventWrapper);
            throw new EventPublishingException($"{GrainId.ToString()} failed to send event downwards", ex);
        }
    }
}