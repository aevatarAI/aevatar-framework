using System.Diagnostics.Metrics;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Observability;

public static class EventPublishLatencyMetrics
{
    private static readonly Meter Meter = new(OpenTelemetryConstants.AevatarStreamsMeterName);
    private static readonly Histogram<double> PublishLatencyHistogram = Meter.CreateHistogram<double>(
        OpenTelemetryConstants.EventPublishLatencyHistogram, "s", "Event publish-to-consume latency");

    public static void Record(double latency, EventWrapperBase item, string? methodName, string? parameterTypeName, ILogger? logger = null)
    {
        var eventType = item.GetType().Name;
        var grainId = item.GetType().GetProperty("GrainId")?.GetValue(item)?.ToString() ?? "unknown";
        var eventId = item.GetType().GetProperty("EventId")?.GetValue(item)?.ToString() ?? "unknown";
        PublishLatencyHistogram.Record(latency,
            new KeyValuePair<string, object?>("grain_id", grainId),
            new KeyValuePair<string, object?>("method_name", methodName ?? "unknown"),
            new KeyValuePair<string, object?>("parameter_type", parameterTypeName ?? "unknown"));
        logger?.LogInformation("[PublishLatency] latency={Latency}s event_type={EventType} grain_id={GrainId} event_id={EventId} method={MethodName} parameter={ParameterType}",
            latency, eventType, grainId, eventId, methodName ?? "unknown", parameterTypeName ?? "unknown");
    }
} 