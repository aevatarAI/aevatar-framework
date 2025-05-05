using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
{
    protected sealed override void TransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        GAgentTransitionState(state, @event);

        Logger.LogInformation("GrainId {GrainId}: State before transition: {@State}", this.GetGrainId().ToString(),
            State);

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

    [GenerateSerializer]
    public class AddChildStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Child { get; set; }
    }


    [GenerateSerializer]
    public class RemoveChildStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public GrainId Child { get; set; }
    }
}