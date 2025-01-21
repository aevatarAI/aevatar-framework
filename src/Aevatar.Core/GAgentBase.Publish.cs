using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent>
{
    private Guid? _correlationId;

    private async Task PublishAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        await SendEventUpwardsAsync(eventWrapper);
        await SendEventDownwardsAsync(eventWrapper);
    }

    protected async Task<Guid> PublishAsync<T>(T @event) where T : EventBase
    {
        _correlationId ??= Guid.NewGuid();
        @event.CorrelationId = _correlationId;
        @event.PublisherGrainId = this.GetGrainId();
        @event.Children = State.Children;
        Logger.LogInformation("Published event {@Event}, {CorrelationId}", @event, _correlationId);

        var eventId = Guid.NewGuid();
        if (State.Parent == null)
        {
            Logger.LogInformation(
                "Event is the first time appeared to silo: {@Event}", @event);
            // This event is the first time appeared to silo.
            await SendEventToSelfAsync(new EventWrapper<T>(@event, eventId, this.GetGrainId()));
        }
        else
        {
            Logger.LogInformation(
                "{GrainId} is publishing event upwards: {EventJson}",
                this.GetGrainId().ToString(), JsonConvert.SerializeObject(@event));
            await SendEventUpwardsAsync(new EventWrapper<T>(@event, eventId, this.GetGrainId()));
        }

        return eventId;
    }

    private async Task SendEventUpwardsAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        var parent = State.Parent.ToString();
        if (parent == null) return;
        var stream = GetStream(parent);
        await stream.OnNextAsync(eventWrapper);
    }

    private async Task SendEventToSelfAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        await GetStream(this.GetGrainId().ToString()).OnNextAsync(eventWrapper);
    }

    private async Task SendEventDownwardsAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        var children = eventWrapper.Children ?? State.Children;
        foreach (var grainId in children)
        {
            var gAgent = GrainFactory.GetGrain<IGAgent>(grainId);
            await gAgent.ActivateAsync();
            var stream = GetStream(grainId.ToString());
            await stream.OnNextAsync(eventWrapper);
        }
    }
}