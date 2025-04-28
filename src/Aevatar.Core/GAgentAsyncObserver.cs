using Aevatar.Core.Abstractions;
using Orleans.Streams;
using OrleansCodeGen.Orleans.Runtime;
using System.Diagnostics;

namespace Aevatar.Core;

public class GAgentAsyncObserver : IAsyncObserver<EventWrapperBase>
{
    private readonly List<EventWrapperBaseAsyncObserver> _observers;
    private readonly string _grainId;
    private readonly IStreamProvider _streamProvider;

    public GAgentAsyncObserver(List<EventWrapperBaseAsyncObserver> observers, string grainId, IStreamProvider streamProvider)
    {
        _observers = observers;
        _grainId = grainId;
        _streamProvider = streamProvider;
    }
    
    /// <summary>
    /// Finds observers that match the given event type
    /// </summary>
    private List<EventWrapperBaseAsyncObserver> FindMatchingObservers(EventBase eventType)
    {
        return _observers.Where(observer =>
            observer.ParameterTypeName == eventType.GetType().Name ||
            observer.ParameterTypeName == nameof(EventWrapperBase) ||
            observer.MethodName == AevatarGAgentConstants.ForwardEventMethodName ||
            observer.MethodName == AevatarGAgentConstants.ConfigDefaultMethodName).ToList();
    }
    
    /// <summary>
    /// Records an exception in the appropriate tracing context
    /// </summary>
    private void RecordExceptionInTracing(Exception ex, OpenTelemetryScope? scope, Activity? activity)
    {
        if (scope != null)
            scope.RecordException(ex);
        else if (activity != null)
            ActivityHelper.RecordException(activity, ex);
    }
    
    /// <summary>
    /// Processes an event through matching observers with tracing
    /// </summary>
    private async Task ProcessEventThroughObservers(EventWrapperBase item, EventBase eventType)
    {
        var matchedObservers = FindMatchingObservers(eventType);
        foreach (var observer in matchedObservers)
        {
            // TODO: consider adding individual observer-level tracing here
            await observer.OnNextAsync(item);
        }
    }
    
    /// <summary>
    /// Broadcasts a message to all observers
    /// </summary>
    private async Task BroadcastToObservers(Func<EventWrapperBaseAsyncObserver, Task> action)
    {
        foreach (var observer in _observers)
        {
            await action(observer);
        }
    }

    /// <summary>
    /// Publishes an exception to the exception stream
    /// </summary>
    private async Task PublishExceptionAsync(Exception exception, EventWrapperBase item, string sourceMethod)
    {
        try
        {
            // Extract event information if available
            var (eventType, eventId) = EventWrapperHelper.ExtractProperties(item);
            
            // Create exception event with contextual information
            var exceptionEvent = GAgentExceptionEvent.FromException(
                exception,
                GrainId.Parse(_grainId),
                sourceMethod,
                eventId,
                eventType?.GetType().Name
            );
            
            // Copy context metadata from the original event
            foreach (var kvp in item.ContextMetadata)
            {
                exceptionEvent.ContextData[kvp.Key] = kvp.Value;
            }
            
            // Add additional context about where the exception occurred
            exceptionEvent.ContextData[AevatarGAgentConstants.ExceptionSourceContextKey] = "GAgentAsyncObserver.OnNextAsync";
            
            // Create a wrapper for the exception event
            var exceptionEventWrapper = new EventWrapper<GAgentExceptionEvent>(
                exceptionEvent,
                Guid.NewGuid(),
                GrainId.Parse(_grainId)
            );
            
            // Get or create the exception stream
            var streamId = StreamId.Create(
                AevatarGAgentConstants.DefaultExceptionStreamNamespace,
                Guid.Parse(_grainId)
            );
            var exceptionStream = _streamProvider.GetStream<EventWrapperBase>(streamId);
            
            // Publish the exception to the stream
            await exceptionStream.OnNextAsync(exceptionEventWrapper);
        }
        catch (Exception ex)
        {
            // If we fail to publish the exception, just log it - don't throw another exception
            Activity.Current?.AddTag("exception.publish.error", ex.Message);
        }
    }

    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        // Extract event and ID from wrapper
        var (eventType, eventId) = EventWrapperHelper.ExtractProperties(item);
        
        // Try to create an activity with parent context if available
        var activity = ActivityHelper.CreateEventActivity(item, eventType, _grainId, eventId, token);
        
        // If no activity was created, fall back to the existing scope
        using var scope = activity != null ? 
            null : // We already have an activity from the extracted context
            OpenTelemetryScope.Start(_grainId, eventId, eventType, token);

        try
        {
            await ProcessEventThroughObservers(item, eventType);
        }
        catch (Exception ex)
        {
            // Record the exception in tracing
            RecordExceptionInTracing(ex, scope, activity);
            
            // Publish the exception to the exception stream
            await PublishExceptionAsync(ex, item, "ProcessEventThroughObservers");
            
            // Re-throw the exception after publishing
            throw;
        }
        finally
        {
            // Dispose of the activity if we created one
            activity?.Dispose();
        }
    }

    public async Task OnCompletedAsync()
    {
        await BroadcastToObservers(observer => observer.OnCompletedAsync());
    }

    public async Task OnErrorAsync(Exception ex)
    {
        await BroadcastToObservers(observer => observer.OnErrorAsync(ex));
    }
}