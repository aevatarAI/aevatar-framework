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
    public Dictionary<string, string> Descriptions { get; set; }

    [BsonElement("LoadStatus")]
    public Dictionary<string, Aevatar.Core.Abstractions.Plugin.PluginLoadStatus> LoadStatus { get; set; } = new();
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