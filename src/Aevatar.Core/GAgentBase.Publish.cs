using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TGEvent>
{
    private Guid? _correlationId;

    protected async Task PublishAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        await SendEventUpwardsAsync(eventWrapper);
        await SendEventDownwardsAsync(eventWrapper);
    }

    protected async Task<Guid> PublishAsync<T>(T @event) where T : EventBase
    {
        _correlationId ??= Guid.NewGuid();
        @event.CorrelationId = _correlationId;
        Logger.LogInformation("Published event {@Event}, {CorrelationId}", @event, _correlationId);

        var eventId = Guid.NewGuid();
        if (State.Parent.IsDefault)
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
            await PublishEventUpwardsAsync(@event, eventId);
        }

        return eventId;
    }

    private async Task PublishEventUpwardsAsync<T>(T @event, Guid eventId) where T : EventBase
    {
        await SendEventUpwardsAsync(new EventWrapper<T>(@event, eventId, this.GetGrainId()));
    }

    private async Task SendEventUpwardsAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        var stream = GetStream(State.Parent.ToString());
        await stream.OnNextAsync(eventWrapper);
    }

    private async Task SendEventToSelfAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        Logger.LogInformation(
            $"{this.GetGrainId().ToString()} is sending event to self: {JsonConvert.SerializeObject(eventWrapper)}");
        var streamOfThisGAgent = GetStream(this.GetGrainId().ToString());
        await streamOfThisGAgent.OnNextAsync(eventWrapper);
    }

    private async Task SendEventDownwardsAsync<T>(EventWrapper<T> eventWrapper) where T : EventBase
    {
        if (State.Children.IsNullOrEmpty())
        {
            return;
        }

        Logger.LogInformation($"{this.GetGrainId().ToString()} has {State.Children.Count} children.");

        foreach (var grainId in State.Children)
        {
            var gAgent = GrainFactory.GetGrain<IGAgent>(grainId);
            await gAgent.ActivateAsync();
            var stream = GetStream(grainId.ToString());
            await stream.OnNextAsync(eventWrapper);
        }
    }
}