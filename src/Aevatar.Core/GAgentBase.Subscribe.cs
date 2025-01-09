using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent>
{
    
    
    private async Task AddChildAsync(GrainId grainId)
    {
        if (State.Children.Contains(grainId))
        {
            Logger.LogError($"Cannot add duplicate child {grainId}.");
            return;
        }

        base.RaiseEvent(new AddChildStateLogEvent<TStateLogEvent>
        {
            Child = grainId
        });
        await ConfirmEvents();
    }  

    private async Task RemoveChildAsync(GrainId grainId)
    {
        if (!State.Children.IsNullOrEmpty())
        {
            base.RaiseEvent(new RemoveChildStateLogEvent
            {
                Child = grainId
            });
            await ConfirmEvents();
        }
    }
    
    
    [GenerateSerializer]
    public class RemoveChildStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Child { get; set; }
    }

    
    
    //TODO: move to interface
    [GenerateSerializer]
    public class SetParentStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Parent { get; set; }
    }
    
    private async Task SetParentAsync(GrainId grainId)
    {
        base.RaiseEvent(new SetParentStateLogEvent
        {
            Parent = grainId
        });
        await ConfirmEvents();
    }
}