using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Orleans.Runtime;

namespace Aevatar.EventSourcing.MongoDB.Tests;

public static class IdSpanBsonSerializerConfig
{
    private static readonly object LockObj = new();
    private static bool _isRegistered;

    public static void Configure()
    {
        if (_isRegistered) return;
        
        lock (LockObj)
        {
            if (_isRegistered) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(IdSpan)))
            {
                BsonSerializer.RegisterSerializer(new IdSpanBsonSerializer());
            }

            _isRegistered = true;
        }
    }
}