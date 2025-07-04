using System;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;

namespace Aevatar.EventSourcing.MongoDB.Serializers;

/// <summary>
/// Compatible grain storage serializer that can read both Memory format (JSON strings) and MongoDB format (BSON)
/// This enables migration from InMemory storage to MongoDB storage
/// </summary>
public class CompatibleGrainSerializer : IGrainStateSerializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompatibleGrainSerializer"/> class.
    /// </summary>
    public CompatibleGrainSerializer()
    {
        // Register serializers for Orleans specific types
        BsonSerializer.TryRegisterSerializer(new GrainTypeBsonSerializer());
        BsonSerializer.TryRegisterSerializer(new IdSpanBsonSerializer());
    }

    /// <summary>
    /// Deserializes the provided value to the specified type.
    /// Supports both Memory format (JSON string) and MongoDB format (BSON document)
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

        // Check if this is a Memory format (JSON string)
        if (value.IsString)
        {
            // This is from InMemory storage - deserialize from JSON string
            var jsonString = value.AsString;
            return JsonSerializer.Deserialize<T>(jsonString)!;
        }
        
        // Check if this is a MongoDB native format (BSON document)
        if (value.IsBsonDocument)
        {
            // This is native MongoDB BSON format
            return BsonSerializer.Deserialize<T>(value.AsBsonDocument);
        }

        // Fallback: try to deserialize as BSON document
        try
        {
            return BsonSerializer.Deserialize<T>(value.AsBsonDocument);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unable to deserialize value of type {value.BsonType} to {typeof(T).Name}. " +
                $"Expected either a JSON string (Memory format) or BSON document (MongoDB format).", ex);
        }
    }

    /// <summary>
    /// Serializes an object to BSON format (MongoDB native format)
    /// New data will always be stored in MongoDB native format for optimal performance
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