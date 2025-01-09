using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TGEvent>
{
    private async Task AddChildAsync(GrainId grainId)
    {
        if (State.Children.Contains(grainId))
        {
            Logger.LogError($"Cannot add duplicate child {grainId}.");
            return;
        }

        base.RaiseEvent(new AddChildGEvent
        {
            Child = grainId
        });
        await ConfirmEvents();
    }  

    private async Task RemoveChildAsync(GrainId grainId)
    {
        if (!State.Children.IsNullOrEmpty())
        {
            base.RaiseEvent(new RemoveChildGEvent
            {
                Child = grainId
            });
            await ConfirmEvents();
        }
    }

    private async Task SetParentAsync(GrainId grainId)
    {
        base.RaiseEvent(new SetParentGEvent
        {
            Parent = grainId
        });
        await ConfirmEvents();
    }
}