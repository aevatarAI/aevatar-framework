using Aevatar.Plugins.Entities;
using Volo.Abp.Domain.Repositories;

namespace Aevatar.Plugins.Repositories;

public interface IPluginCodeStorageRepository : IRepository<PluginCodeStorageSnapshotDocument, string>
{
    Task<byte[]?> GetPluginCodeByGAgentPrimaryKey(Guid primaryKey);
    Task<Dictionary<Type, string>> GetPluginDescriptionsByGAgentPrimaryKey(Guid primaryKey);
    Task<IReadOnlyList<byte[]>> GetPluginCodesByGAgentPrimaryKeys(IReadOnlyList<Guid> primaryKeys);
}