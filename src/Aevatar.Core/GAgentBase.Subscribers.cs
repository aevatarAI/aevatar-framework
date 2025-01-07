using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TEvent>
{
    private async Task AddSubscriberAsync(GrainId grainId)
    {
        if (State.Subscribers.Contains(grainId))
        {
            Logger.LogError($"Cannot add duplicate subscriber {grainId}.");
            return;
        }

        base.RaiseEvent(new AddSubscriberGEvent
        {
            Subscriber = grainId
        });
        await ConfirmEvents();
    }  

    private async Task RemoveSubscriberAsync(GrainId grainId)
    {
        if (!State.Subscribers.IsNullOrEmpty())
        {
            base.RaiseEvent(new RemoveSubscriberGEvent
            {
                Subscriber = grainId
            });
            await ConfirmEvents();
        }
    }

    private async Task SetSubscriptionAsync(GrainId grainId)
    {
        base.RaiseEvent(new SetSubscriptionGEvent
        {
            Subscription = grainId
        });
        await ConfirmEvents();
    }
}