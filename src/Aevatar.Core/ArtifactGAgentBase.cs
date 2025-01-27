using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

[GAgent]
public abstract class ArtifactGAgentBase<TArtifact, TState, TStateLogEvent>(ILogger logger)
    : ArtifactGAgentBase<TArtifact, TState, TStateLogEvent, EventBase, ConfigurationBase>(logger)
    where TArtifact : IArtifact<TState, TStateLogEvent>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>;

[GAgent]
public abstract class ArtifactGAgentBase<TArtifact, TState, TStateLogEvent, TEvent, TConfiguration>
    : GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
    where TArtifact : IArtifact<TState, TStateLogEvent>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
    where TConfiguration : ConfigurationBase
{
    private readonly TArtifact _artifact;

    public ArtifactGAgentBase(ILogger logger) : base(logger)
    {
        _artifact = ActivatorUtilities.CreateInstance<TArtifact>(ServiceProvider);
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult(_artifact.GetDescription());
    }

    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnGAgentActivateAsync(cancellationToken);
        await UpdateObserverListAsync(_artifact.GetType());
    }

    protected override void GAgentTransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        base.GAgentTransitionState(state, @event);
        _artifact.TransitionState(state, @event);
    }

    public override async Task<List<Type>?> GetAllSubscribedEventsAsync(bool includeBaseHandlers = false)
    {
        var gAgentEvents = await base.GetAllSubscribedEventsAsync(includeBaseHandlers);
        var artifactEventHandlerMethods = GetEventHandlerMethods(_artifact.GetType());
        var handlingTypes = artifactEventHandlerMethods
            .Select(m => m.GetParameters().First().ParameterType);
        return handlingTypes.Concat(gAgentEvents!).ToList();
    }
}