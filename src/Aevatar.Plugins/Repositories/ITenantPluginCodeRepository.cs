using Aevatar.Plugins.Entities;
using Volo.Abp.Domain.Repositories;

namespace Aevatar.Plugins.Repositories;

public interface ITenantPluginCodeRepository : IRepository<TenantPluginCodeSnapshotDocument, string>
{
    Task<IReadOnlyList<Guid>?> GetGAgentPrimaryKeysByTenantIdAsync(Guid tenantId);
}