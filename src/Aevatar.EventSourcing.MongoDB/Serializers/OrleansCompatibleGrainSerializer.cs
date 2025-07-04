using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;

namespace Aevatar.EventSourcing.MongoDB.Serializers;

/// <summary>
/// Orleans-compatible grain storage serializer that can handle:
/// 1. Memory format (individual events as JSON strings)
/// 2. Orleans LogStateWithMetaData format (complete state objects)
/// 3. MongoDB format (BSON documents)
/// </summary>
public class OrleansCompatibleGrainSerializer : IGrainStateSerializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrleansCompatibleGrainSerializer"/> class.
    /// </summary>
    public OrleansCompatibleGrainSerializer()
    {
        // Register serializers for Orleans specific types
        BsonSerializer.TryRegisterSerializer(new GrainTypeBsonSerializer());
        BsonSerializer.TryRegisterSerializer(new IdSpanBsonSerializer());
    }

    /// <summary>
    /// Deserializes the provided value, handling multiple Orleans storage formats
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="value">BSON value to deserialize</param>
    /// <returns>Deserialized object</returns>
    public T Deserialize<T>(BsonValue value)
    {
        if (value == null || value.IsBsonNull)
        {
            return default(T)!;
        }

        // Case 1: MongoDB native format (BSON document)
        if (value.IsBsonDocument)
        {
            return BsonSerializer.Deserialize<T>(value.AsBsonDocument);
        }

        // Case 2: JSON string format (Memory storage or Orleans serialization)
        if (value.IsString)
        {
            var jsonString = value.AsString;
            return DeserializeFromJson<T>(jsonString);
        }

        // Fallback
        throw new InvalidOperationException(
            $"Unable to deserialize value of type {value.BsonType} to {typeof(T).Name}. " +
            $"Expected either a JSON string or BSON document.");
    }

    private T DeserializeFromJson<T>(string jsonString)
    {
        try
        {
            // First, try to detect if this is an Orleans LogStateWithMetaData object
            if (IsOrleansLogStateWithMetaData(jsonString))
            {
                return DeserializeOrleansLogState<T>(jsonString);
            }

            // Otherwise, treat as simple JSON object
            return JsonSerializer.Deserialize<T>(jsonString)!;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize JSON string to {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private bool IsOrleansLogStateWithMetaData(string jsonString)
    {
        // Quick check for Orleans LogStateWithMetaData structure
        return jsonString.Contains("\"__type\"") && 
               jsonString.Contains("LogStateWithMetaData") &&
               jsonString.Contains("\"Log\"") &&
               jsonString.Contains("\"GlobalVersion\"");
    }

    private T DeserializeOrleansLogState<T>(string jsonString)
    {
        try
        {
            // Parse as JsonDocument to extract the Log array
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("Log", out var logProperty))
            {
                // Check if Log has __values property (Orleans serialization format)
                if (logProperty.TryGetProperty("__values", out var valuesProperty))
                {
                    // Extract events from __values array
                    var events = ExtractEventsFromValues<T>(valuesProperty);
                    return events;
                }
                else if (logProperty.ValueKind == JsonValueKind.Array)
                {
                    // Direct array format
                    var events = JsonSerializer.Deserialize<T>(logProperty.GetRawText())!;
                    return events;
                }
            }

            // If we can't extract Log, fallback to full deserialization
            return JsonSerializer.Deserialize<T>(jsonString)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize Orleans LogStateWithMetaData to {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private T ExtractEventsFromValues<T>(JsonElement valuesElement)
    {
        // If T is a collection type, extract all events
        if (typeof(T).IsAssignableFrom(typeof(List<object>)) || 
            typeof(T).IsAssignableFrom(typeof(IList<object>)) ||
            typeof(T).IsAssignableFrom(typeof(IEnumerable<object>)))
        {
            var events = new List<object>();
            foreach (var eventElement in valuesElement.EnumerateArray())
            {
                var eventObj = JsonSerializer.Deserialize<object>(eventElement.GetRawText());
                if (eventObj != null)
                {
                    events.Add(eventObj);
                }
            }
            return (T)(object)events;
        }

        // If T is a single object type, take the first event
        if (valuesElement.GetArrayLength() > 0)
        {
            var firstEvent = valuesElement[0];
            return JsonSerializer.Deserialize<T>(firstEvent.GetRawText())!;
        }

        return default(T)!;
    }

    /// <summary>
    /// Serializes an object to BSON format (MongoDB native format)
    /// Always writes in MongoDB format for optimal performance
    /// </summary>
    /// <typeparam name="T">Type of object to serialize</typeparam>
    /// <param name="state">Object to serialize</param>
    /// <returns>BSON representation of the object</returns>
    public BsonValue Serialize<T>(T state)
    {
        if (state == null)
        {
            return BsonNull.Value;
        }

        // Always serialize new data as BSON for optimal MongoDB performance
        return state.ToBsonDocument();
    }
}

/// <summary>
/// Helper class for Orleans LogStateWithMetaData structure
/// </summary>
public class OrleansLogStateWithMetaData
{
    public OrleansLogContainer? Log { get; set; }
    public int GlobalVersion { get; set; }
    public string? WriteVector { get; set; }
}

public class OrleansLogContainer
{
    public List<object>? __values { get; set; }
    public string? __type { get; set; }
}