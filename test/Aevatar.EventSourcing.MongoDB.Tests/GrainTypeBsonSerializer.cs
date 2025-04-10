using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Orleans.Runtime;

namespace Aevatar.EventSourcing.MongoDB.Tests;

public static class GrainTypeBsonSerializerConfig
{
    private static readonly object LockObj = new();
    private static bool _isRegistered;

    public static void Configure()
    {
        if (_isRegistered) return;
        
        lock (LockObj)
        {
            if (_isRegistered) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(GrainType)))
            {
                BsonSerializer.RegisterSerializer(new GrainTypeBsonSerializer());
            }

            _isRegistered = true;
        }
    }
}