using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
{
    protected sealed override void TransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        switch (@event)
        {
            case AddChildStateLogEvent addChildEvent:
                Logger.LogDebug("GrainId {GrainId}: Adding child {Child}", this.GetGrainId().ToString(), addChildEvent.Child);
                State.Children.Add(addChildEvent.Child);
                break;
            case AddChildManyStateLogEvent addChildManyEvent:
                Logger.LogDebug("GrainId {GrainId}: Adding children {Child}", this.GetGrainId().ToString(), addChildManyEvent.Children);
                State.Children.AddRange(addChildManyEvent.Children);
                break;
            case RemoveChildStateLogEvent removeChildEvent:
                Logger.LogDebug("GrainId {GrainId}: Removing child {Child}", this.GetGrainId().ToString(), removeChildEvent.Child);
                State.Children.Remove(removeChildEvent.Child);
                break;
            case SetParentStateLogEvent setParentEvent:
                Logger.LogDebug("GrainId {GrainId}: Setting parent to {Parent}", this.GetGrainId().ToString(), setParentEvent.Parent);
                State.Parent = setParentEvent.Parent;
                break;
            case ClearParentStateLogEvent clearParentEvent:
                Logger.LogDebug("GrainId {GrainId}: Clearing parent {Parent}", this.GetGrainId().ToString(), clearParentEvent.Parent);
                if (State.Parent == clearParentEvent.Parent)
                    State.Parent = default;
                break;
        }
        
        GAgentTransitionState(state, @event);
        
        Logger.LogInformation("GrainId {GrainId}: State before transition: {@State}", this.GetGrainId().ToString(), State);
        
        base.TransitionState(state, @event);
        
        // print out the state after transition
        Logger.LogDebug("GrainId {GrainId}: State after transition: {@State}", this.GetGrainId().ToString(), State);
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
        
        Logger.LogDebug("GrainId [{GrainId}] Adding child to {Parent}", this.GetGrainId().ToString(), grainId);

        base.RaiseEvent(new AddChildStateLogEvent
        {
            Child = grainId
        });
        await ConfirmEvents();
    }

    private async Task AddChildManyAsync(List<GrainId> grainIds)
    {    
        base.RaiseEvent(new AddChildManyStateLogEvent
        {
            Children = grainIds
        });
        await ConfirmEvents();
    }

    [GenerateSerializer]
    public class AddChildStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Child { get; set; }
    }

    [GenerateSerializer]
    public class AddChildManyStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public required List<GrainId> Children { get; set; }
    }

    private async Task RemoveChildAsync(GrainId grainId)
    {
        Logger.LogDebug("GrainId [{GrainId}] Removing child to {Parent}", this.GetGrainId().ToString(), grainId);
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
        Logger.LogDebug("GrainId [{GrainId}] Setting parent to {Parent}", this.GetGrainId().ToString(), grainId);
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
        Logger.LogDebug("GrainId [{GrainId}] Removing parent to {Parent}", this.GetGrainId().ToString(), grainId);
        base.RaiseEvent(new ClearParentStateLogEvent
        {
            Parent = grainId
        });
        await ConfirmEvents();
    }
}