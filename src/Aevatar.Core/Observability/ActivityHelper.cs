using Aevatar.Core.Abstractions;
using System.Diagnostics;
using Orleans.Streams;

namespace Aevatar.Core;

/// <summary>
/// Helper class for Activity (span) management and tag standardization
/// </summary>
internal static class ActivityHelper
{
    private static readonly ActivitySource ActivitySource = new ActivitySource(OpenTelemetryConstants.ActivitySourceName);
    
    /// <summary>
    /// Creates a new Activity for processing an event
    /// </summary>
    public static Activity? StartMessageProcessingActivity(
        string eventTypeName,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext? parentContext = null)
    {
        var name = $"{OpenTelemetryConstants.MessageProcessSpanNamePrefix}/{eventTypeName}";
        
        // Create activity with appropriate parent context
        return parentContext.HasValue
            ? ActivitySource.StartActivity(name, kind, parentContext.Value)
            : ActivitySource.StartActivity(name, kind);
    }
    
    /// <summary>
    /// Sets all standard tags for a messaging event on an Activity
    /// </summary>
    public static void SetStandardMessageTags(
        Activity? activity,
        string consumerGrainId,
        EventBase? @event = null,
        string? eventId = null,
        StreamSequenceToken? token = null)
    {
        if (activity == null) return;
        
        // Set system and operation tags
        activity.SetTag(OpenTelemetryConstants.MessagingSystemTag, OpenTelemetryConstants.AevatarSystem);
        activity.SetTag(OpenTelemetryConstants.OperationTag, OpenTelemetryConstants.ProcessOperation);
        activity.SetTag(OpenTelemetryConstants.DestinationKindTag, OpenTelemetryConstants.GrainDestination);
        
        // Set event metadata if available
        if (@event != null)
        {
            activity.SetTag(OpenTelemetryConstants.CorrelationIdTag, @event.CorrelationId);
            activity.SetTag(OpenTelemetryConstants.EventTypeTag, @event.GetType().FullName);
            activity.SetTag(OpenTelemetryConstants.PublisherGrainIdTag, @event.PublisherGrainId);
        }
        
        // Set EventId if available
        if (!string.IsNullOrEmpty(eventId))
        {
            activity.SetTag(OpenTelemetryConstants.EventIdTag, eventId);
        }
        
        // Set consumer grain id
        activity.SetTag(OpenTelemetryConstants.ConsumerGrainIdTag, consumerGrainId);
        
        // Set sequence number if available
        if (token != null)
        {
            activity.SetTag(OpenTelemetryConstants.SequenceNumberTag, token.SequenceNumber.ToString());
        }
        
        // Add timestamp
        activity.SetTag(OpenTelemetryConstants.TimestampTag, DateTimeOffset.UtcNow.ToString("o"));
    }
    
    /// <summary>
    /// Records exception details on an Activity
    /// </summary>
    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null) return;
        
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag(OpenTelemetryConstants.ErrorTag, true);
        activity.SetTag(OpenTelemetryConstants.ErrorTypeTag, ex.GetType().FullName);
        activity.SetTag(OpenTelemetryConstants.ErrorMessageTag, ex.Message);
        activity.SetTag(OpenTelemetryConstants.ErrorStackTraceTag, ex.StackTrace);
    }
    
    /// <summary>
    /// Attempts to extract parent context from event metadata
    /// </summary>
    public static ActivityContext? ExtractParentContext(EventWrapperBase item)
    {
        if (item.ContextMetadata == null || item.ContextMetadata.Count == 0)
            return null;
            
        // Try to extract parent context from metadata
        if (!item.ContextMetadata.TryGetValue(EventWrapperBase.TraceIdKey, out var traceIdStr) ||
            !item.ContextMetadata.TryGetValue(EventWrapperBase.SpanIdKey, out var spanIdStr))
            return null;
            
        try
        {
            // Parse trace ID and span ID
            var traceId = ActivityTraceId.CreateFromString(traceIdStr);
            var spanId = ActivitySpanId.CreateFromString(spanIdStr);
            
            // Parse trace flags if available
            ActivityTraceFlags traceFlags = ActivityTraceFlags.None;
            if (item.ContextMetadata.TryGetValue(EventWrapperBase.TraceFlagsKey, out var traceFlagsStr))
            {
                Enum.TryParse(traceFlagsStr, out traceFlags);
            }

            return new ActivityContext(traceId, spanId, traceFlags, isRemote: true);
        }
        catch (Exception)
        {
            // Failed to parse trace ID or span ID
            return null;
        }
    }
    
    /// <summary>
    /// Applies baggage items from event metadata to activity
    /// </summary>
    public static void ApplyBaggageItems(Activity? activity, EventWrapperBase item)
    {
        if (activity == null || item.ContextMetadata == null)
            return;
            
        foreach (var entry in item.ContextMetadata.Where(x => x.Key.StartsWith(EventWrapperBase.BaggagePrefixKey)))
        {
            var baggageKey = entry.Key.Substring(EventWrapperBase.BaggagePrefixKey.Length);
            activity.AddBaggage(baggageKey, entry.Value);
        }
    }
    
    /// <summary>
    /// Creates a complete tracing activity from an event wrapper with all context and tags
    /// </summary>
    public static Activity? CreateEventActivity(EventWrapperBase item, EventBase eventType, string grainId, string? eventId, StreamSequenceToken? token)
    {
        var parentContext = ExtractParentContext(item);
        if (!parentContext.HasValue)
            return null;
        
        // Start activity with extracted parent context and set all standard tags
        var activity = StartMessageProcessingActivity(
            eventType.GetType().FullName ?? "UnknownEvent", 
            ActivityKind.Internal, 
            parentContext);
            
        SetStandardMessageTags(activity, grainId, eventType, eventId, token);
        
        // Apply baggage items if any
        ApplyBaggageItems(activity, item);
        
        return activity;
    }
} 