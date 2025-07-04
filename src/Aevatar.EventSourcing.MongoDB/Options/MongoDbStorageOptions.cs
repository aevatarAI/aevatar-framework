using MongoDB.Driver;
using Orleans.Storage;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;

namespace Aevatar.EventSourcing.MongoDB.Options;

public class MongoDbStorageOptions : IStorageProviderSerializerOptions
{
    public int InitStage { get; set; } = ServiceLifecycleStage.ApplicationServices;

    /// <summary>
    /// This is not in use, it is only for compatibility
    /// </summary>
    public IGrainStorageSerializer GrainStorageSerializer { get; set; } = null!;

    /// <summary>
    /// The serializer to use for the grain state.
    /// </summary>
    public IGrainStateSerializer GrainStateSerializer { get; set; } = null!;

    [Redact] public MongoClientSettings ClientSettings { get; set; } = null!;

    public string? Database { get; set; }

    [Redact] public MongoCredential? Credentials { get; set; }
}