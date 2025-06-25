// ReSharper disable once CheckNamespace

using System.Diagnostics;

namespace Aevatar.Core.Abstractions;

using System.Collections.Generic;

[GenerateSerializer]
public abstract class EventWrapperBase
{
    // Constants for context metadata keys
    public const string TraceIdKey = "TraceId";
    public const string SpanIdKey = "SpanId";
    public const string TraceFlagsKey = "TraceFlags";
    public const string BaggagePrefixKey = "Baggage.";
    
    [Id(0)]
    public Dictionary<string, string> ContextMetadata { get; set; } = new Dictionary<string, string>();

    [Id(1)]
    public DateTime PublishedTimestampUtc { get; set; }

    protected EventWrapperBase()
    {
        // Initialize ContextMetadata
        ContextMetadata = new Dictionary<string, string>();

        // Simple context injection - in real implementation, this would be
        // enhanced with DistributedContextPropagator usage in GAgentAsyncObserver
        var activity = Activity.Current;
        if (activity != null)
        {
            ContextMetadata[TraceIdKey] = activity.TraceId.ToString();
            ContextMetadata[SpanIdKey] = activity.SpanId.ToString();
            ContextMetadata[TraceFlagsKey] = activity.ActivityTraceFlags.ToString();

            // Add baggage items
            foreach (var baggage in activity.Baggage)
            {
                ContextMetadata[$"{BaggagePrefixKey}{baggage.Key}"] = baggage.Value;
            }
        }
    }
}