using Aevatar.EventSourcing.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Volo.Abp.Domain.Entities;

namespace Aevatar.Plugins.Entities;

[BsonIgnoreExtraElements]
public class PluginCodeStorageSnapshotDocument: Entity<string>
{
    [BsonElement("_etag")]
    public string Etag { get; set; }

    [BsonElement("_doc")]
    public PluginCodeStorageDoc Doc { get; set; }
}

[BsonIgnoreExtraElements]
public class PluginCodeStorageDoc
{
    [BsonElement("__id")]
    public string InternalId { get; set; }

    [BsonElement("__type")]
    public string Type { get; set; }

    [BsonElement("Snapshot")]
    public PluginCodeStorageSnapshot Snapshot { get; set; }
}

[BsonIgnoreExtraElements]
public class PluginCodeStorageSnapshot
{
    [BsonElement("__id")]
    public string InternalId { get; set; }

    [BsonElement("__type")]
    public string Type { get; set; }

    [BsonElement("Code")]
    public ByteArrayContainer Code { get; set; }
    
    [BsonElement("Descriptions")]
    public DescriptionsContainer Descriptions { get; set; }
}

[BsonIgnoreExtraElements]
public class ByteArrayContainer
{
    [BsonElement("__type")]
    public string Type { get; set; }

    [BsonElement("__value")]
    [BsonRepresentation(BsonType.Binary)]
    public byte[] Value { get; set; }
}

public class DescriptionsContainer
{
    [BsonElement("__id")]
    public string InternalId { get; set; }

    [BsonElement("__type")]
    public string Type { get; set; }

    [BsonElement("Entries")]
    public List<DescriptionEntry> Entries { get; set; } = new();
}

public class DescriptionEntry
{
    [BsonElement("Key")]
    [BsonSerializer(typeof(TypeBsonSerializer))]
    public Type Key { get; set; }

    [BsonElement("Value")] public string Value { get; set; }
}