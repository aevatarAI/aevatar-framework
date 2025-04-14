using Aevatar.Core.Abstractions;

namespace Aevatar.Core;

using System.Diagnostics;
using Orleans.Streams;

internal class OpenTelemetryScope : IDisposable
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("Aevatar.Messaging");

    private readonly string _grainId;

    private Activity _activity;

    public static OpenTelemetryScope Start(string grainId, EventBase? @event, StreamSequenceToken? token = null)
    {
        var obj = new OpenTelemetryScope(grainId);
        obj.StartProcessing(@event, token);
        return obj;
    }

    private OpenTelemetryScope(string grainId)
    {
        _grainId = grainId;
    }

    private void StartProcessing(EventBase? @event, StreamSequenceToken? token = null)
    {
        var eventTypeName = @event?.GetType().FullName ?? "UnknownEvent";
        _activity = ActivitySource.StartActivity($"ProcessNextGrainEvent/{eventTypeName}", ActivityKind.Internal);

        // Add event details to the activity
        _activity?.SetTag("event.type", eventTypeName);
        _activity?.SetTag("event.correlation_id", @event?.CorrelationId);
        _activity?.SetTag("event.publisher_grain_id", @event?.PublisherGrainId);
        _activity?.SetTag("event.consumer_grain_id", _grainId);
        _activity?.SetTag("stream.sequence_number", token?.SequenceNumber.ToString());

        // Add more descriptive tags
        _activity?.SetTag("event.timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public void RecordException(Exception ex)
    {
        var errorType = ex.GetType().FullName;

        _activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        _activity?.SetTag("error.type", errorType);
        _activity?.SetTag("error.message", ex.Message);
        _activity?.SetTag("error.stack_trace", ex.StackTrace);
    }

    public void Dispose()
    {
        _activity?.Stop();
    }
}