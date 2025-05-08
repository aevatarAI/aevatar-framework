using Aevatar.Core.Abstractions.Plugin;
using MongoDB.Bson.Serialization.Attributes;
using Volo.Abp.Domain.Entities;

namespace Aevatar.Plugins.Entities;

[BsonIgnoreExtraElements]
public class PluginLoadStatusDocument : Entity<string>
{
    [BsonElement("TenantId")] public Guid TenantId { get; set; }

    [BsonElement("LoadStatus")] public Dictionary<string, PluginLoadStatus> LoadStatus { get; set; } = new();
}