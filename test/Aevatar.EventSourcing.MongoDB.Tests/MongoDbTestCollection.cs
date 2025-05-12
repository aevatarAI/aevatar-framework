using MongoDB.Bson.Serialization;
using Orleans.Runtime;
using Xunit;

namespace Aevatar.EventSourcing.MongoDB.Tests;

[CollectionDefinition(nameof(MongoDbTestCollection))]
public class MongoDbTestCollection : ICollectionFixture<MongoDbTestCollection>
{
    public MongoDbTestCollection()
    {
        GrainTypeBsonSerializerConfig.Configure();
        IdSpanBsonSerializerConfig.Configure();
    }
} 