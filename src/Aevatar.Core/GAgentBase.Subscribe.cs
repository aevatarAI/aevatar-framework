using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent>
{
    protected override void TransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        switch (@event)
        {
            case AddChildStateLogEvent addChildEvent:
                State.Children.Add(addChildEvent.Child);
                break;
            case RemoveChildStateLogEvent removeChildEvent:
                State.Children.Remove(removeChildEvent.Child);
                break;
            case SetParentStateLogEvent setParentEvent:
                State.Parent=setParentEvent.Parent;
                break;
            case InnerSetInitializeDtoTypeStateLogEvent setInnerEvent:
                State.InitializeDtoType = setInnerEvent.InitializeDtoType;
                break;
        }
        base.TransitionState(state, @event);
    }

    private async Task AddChildAsync(GrainId grainId)
    {
        if (State.Children.Contains(grainId))
        {
            Logger.LogError($"Cannot add duplicate child {grainId}.");
            return;
        }

        base.RaiseEvent(new AddChildStateLogEvent
        {
            Child = grainId
        });
        await ConfirmEvents();
    }

    public class AddChildStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Child { get; set; }
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