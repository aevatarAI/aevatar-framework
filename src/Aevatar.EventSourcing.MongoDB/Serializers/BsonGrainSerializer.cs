using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using Orleans;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Runtime;

namespace Aevatar.EventSourcing.MongoDB.Serializers;

/// <summary>
/// Grain storage serializer that uses MongoDB BSON serialization
/// </summary>
public class BsonGrainSerializer : IGrainStateSerializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BsonGrainSerializer"/> class.
    /// </summary>
    public BsonGrainSerializer()
    {
        // Register serializers for Orleans specific types
        BsonSerializer.TryRegisterSerializer(new GrainTypeBsonSerializer());
        BsonSerializer.TryRegisterSerializer(new IdSpanBsonSerializer());
    }

    /// <summary>
    /// Deserializes the provided value to the specified type
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="value">BSON value to deserialize</param>
    /// <returns>Deserialized object</returns>
    public T Deserialize<T>(BsonValue value)
    {       
        return BsonSerializer.Deserialize<T>(value.AsBsonDocument);
    }

    /// <summary>
    /// Serializes an object to BSON format
    /// </summary>
    /// <typeparam name="T">Type of object to serialize</typeparam>
    /// <param name="state">Object to serialize</param>
    /// <returns>BSON representation of the object</returns>
    public BsonValue Serialize<T>(T state)
    {
        return state.ToBsonDocument();
    }
} 