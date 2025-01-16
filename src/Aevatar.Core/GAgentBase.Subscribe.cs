using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent>
{
    protected sealed override void TransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
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
                State.Parent = setParentEvent.Parent;
                break;
            case ClearParentStateLogEvent clearParentEvent:
                if (State.Parent == clearParentEvent.Parent)
                    State.Parent = default;
                break;
            case InnerSetInitializeDtoTypeStateLogEvent setInnerEvent:
                State.InitializationEventType = setInnerEvent.InitializeDtoType;
                break;
        }

        GAgentTransitionState(state, @event);
        base.TransitionState(state, @event);
    }

    protected virtual void GAgentTransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        // Derived classes can override this method.
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

    [GenerateSerializer]
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

    [GenerateSerializer]
    public class ClearParentStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Parent { get; set; }
    }
    
    private async Task ClearParentAsync(GrainId grainId)
    {
        base.RaiseEvent(new ClearParentStateLogEvent
        {
            Parent = grainId
        });
        await ConfirmEvents();
    }
}