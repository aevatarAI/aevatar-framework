// ReSharper disable once CheckNamespace
namespace Aevatar.Core;

/// <summary>
/// Constants for OpenTelemetry tags and values used throughout the application
/// </summary>
public static class OpenTelemetryConstants
{
    // Activity sources
    public const string ActivitySourceName = "Aevatar.Messaging";
    
    // Span names
    public const string MessageProcessSpanNamePrefix = "aevatar.message.process";
    
    // System tags
    public const string MessagingSystemTag = "messaging.system";
    public const string AevatarSystem = "aevatar";
    
    // Operation tags
    public const string OperationTag = "messaging.aevatar.operation";
    public const string ProcessOperation = "process";
    
    // Destination tags
    public const string DestinationKindTag = "messaging.aevatar.destination_kind";
    public const string GrainDestination = "grain";
    
    // Event metadata
    public const string CorrelationIdTag = "messaging.aevatar.correlation_id";
    public const string EventIdTag = "messaging.aevatar.event_id";
    public const string EventTypeTag = "messaging.aevatar.event_type";
    public const string PublisherGrainIdTag = "messaging.aevatar.publisher_grain_id";
    public const string ConsumerGrainIdTag = "messaging.aevatar.consumer_grain_id";
    public const string SequenceNumberTag = "messaging.aevatar.sequence_number";
    public const string TimestampTag = "messaging.timestamp";
    
    // Error tags
    public const string ErrorTag = "error";
    public const string ErrorTypeTag = "error.type";
    public const string ErrorMessageTag = "error.message";
    public const string ErrorStackTraceTag = "error.stack_trace";
} 