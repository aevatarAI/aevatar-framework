using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

[GAgent]
public class ArtifactGAgent<TArtifact, TState, TStateLogEvent>
    : ArtifactGAgent<TArtifact, TState, TStateLogEvent, EventBase, ConfigurationBase>
    where TArtifact : IArtifact<TState, TStateLogEvent>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>;

[GAgent]
public class ArtifactGAgent<TArtifact, TState, TStateLogEvent, TEvent, TConfiguration>
    : GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>, IArtifactGAgent<TArtifact, TState, TStateLogEvent>
    where TArtifact : IArtifact<TState, TStateLogEvent>
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
    where TConfiguration : ConfigurationBase
{
    private readonly TArtifact _artifact;
    private bool _isInitialized;

    public ArtifactGAgent()
    {
        try
        {
            if (ServiceProvider == null)
                throw new InvalidOperationException("ServiceProvider must be initialized");

            _artifact = ActivatorUtilities.CreateInstance<TArtifact>(ServiceProvider);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            throw new ArtifactGAgentException(
                $"Failed to initialize {typeof(TArtifact).Name}", ex);
        }
    }

    public Task<TArtifact> GetArtifactAsync()
    {
        return _isInitialized
            ? Task.FromResult(_artifact)
            : throw new ObjectDisposedException("Artifact not initialized");
    }

    public override Task<string> GetDescriptionAsync()
    {
        ValidateOperationStatus();
        return Task.FromResult(_artifact.GetDescription());
    }

    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        ValidateOperationStatus();
        try
        {
            await base.OnGAgentActivateAsync(cancellationToken)
                .ConfigureAwait(false);

            await UpdateObserverListAsync(_artifact.GetType());
        }
        catch (OperationCanceledException ocEx)
        {
            throw new ArtifactGAgentException(
                $"Activation cancelled for {typeof(TArtifact).Name}", ocEx);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Activation failed: {ex}");
            throw new ArtifactGAgentException(
                $"Critical failure during activation", ex);
        }
    }

    protected override void GAgentTransitionState(
        TState state,
        StateLogEventBase<TStateLogEvent> @event)
    {
        ValidateParameters(state, @event);
        try
        {
            base.GAgentTransitionState(state, @event);
            _artifact.TransitionState(state, @event);
        }
        catch (Exception ex)
        {
            throw new StateTransitionException(
                $"Invalid state transition: {state} with event {@event}",
                ex);
        }
    }

    public override async Task<List<Type>?> GetAllSubscribedEventsAsync(
        bool includeBaseHandlers = false)
    {
        ValidateOperationStatus();
        try
        {
            var gAgentEvents = await base.GetAllSubscribedEventsAsync(includeBaseHandlers)
                .ConfigureAwait(false);

            var artifactMethods = GetEventHandlerMethods(typeof(TArtifact))?
                .Where(m => m != null) ?? [];

            var handlingTypes = artifactMethods
                .Select(m => m.GetParameters().FirstOrDefault()?.ParameterType)
                .Where(t => t != null && t.IsSubclassOf(typeof(TEvent)));

            return handlingTypes
                .Concat(gAgentEvents ?? new List<Type>())
                .Distinct()
                .ToList();
        }
        catch (ReflectionTypeLoadException rtle)
        {
            throw new ArtifactGAgentException(
                $"Type load error: {rtle.LoaderExceptions.First()?.Message}",
                rtle);
        }
    }

    private void ValidateOperationStatus()
    {
        if (!_isInitialized)
            throw new ObjectDisposedException("GAgent instance is not functional");
    }

    private static void ValidateParameters(
        TState state,
        StateLogEventBase<TStateLogEvent> @event)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state),
                "State cannot be null in transition");

        if (@event == null)
            throw new ArgumentNullException(nameof(@event),
                "Transition event cannot be null");
    }
}