using Aevatar.Core.Abstractions;

namespace Aevatar.Core;

using System.Diagnostics;
using Orleans.Streams;

/// <summary>
/// Scope for OpenTelemetry tracing that automatically handles Activity creation and cleanup
/// </summary>
internal class OpenTelemetryScope : IDisposable
{
    private readonly string _grainId;
    private readonly string? _eventId;

    private Activity? _activity;

    /// <summary>
    /// Creates a new OpenTelemetryScope for a grain processing an event
    /// </summary>
    public static OpenTelemetryScope Start(string grainId, string? eventId, EventBase? @event, StreamSequenceToken? token = null)
    {
        var obj = new OpenTelemetryScope(grainId, eventId);
        obj.StartProcessing(@event, token);
        
        // If there's an active Activity, link it
        if (Activity.Current != null && obj._activity != null)
        {
            obj._activity.SetParentId(Activity.Current.TraceId, Activity.Current.SpanId, Activity.Current.ActivityTraceFlags);
        }
        
        return obj;
    }

    private OpenTelemetryScope(string grainId, string? eventId)
    {
        _grainId = grainId;
        _eventId = eventId;
    }

    private void StartProcessing(EventBase? @event, StreamSequenceToken? token = null)
    {
        var eventTypeName = @event?.GetType().FullName ?? "UnknownEvent";
        
        // Create activity
        _activity = ActivityHelper.StartMessageProcessingActivity(eventTypeName);
        
        // Set standard tags
        ActivityHelper.SetStandardMessageTags(_activity, _grainId, @event, _eventId, token);
    }

    public void RecordException(Exception ex)
    {
        ActivityHelper.RecordException(_activity, ex);
    }

    public void Dispose()
    {
        _activity?.Stop();
    }
}