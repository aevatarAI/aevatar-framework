using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins.Entities;
using Volo.Abp.Domain.Repositories;

namespace Aevatar.Plugins.Repositories;

public interface IPluginLoadStatusRepository : IRepository<PluginLoadStatusDocument, string>
{
    /// <summary>
    /// Get plugin load status dictionary by plugin code primary key.
    /// </summary>
    Task<Dictionary<string, PluginLoadStatus>> GetPluginLoadStatusAsync(Guid tenantId);

    /// <summary>
    /// Set plugin load status dictionary by plugin code primary key.
    /// </summary>
    Task SetPluginLoadStatusAsync(Guid tenantId, Dictionary<string, PluginLoadStatus> status);

    Task ClearPluginLoadStatusAsync();
}