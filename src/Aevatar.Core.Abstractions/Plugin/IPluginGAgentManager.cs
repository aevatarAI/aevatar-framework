using System.Reflection;

namespace Aevatar.Core.Abstractions.Plugin;

public interface IPluginGAgentManager
{
    Task<Guid> AddPluginAsync(AddPluginDto addPluginDto);
    Task<IReadOnlyList<Guid>> GetPluginsAsync(Guid tenantId);
    Task<PluginsInformation> GetPluginsWithDescriptionAsync(Guid tenantId);
    Task<Dictionary<Type, string>> GetPluginDescriptions(Guid pluginCodeId);
    Task RemovePluginAsync(RemovePluginDto removePluginDto);
    Task UpdatePluginAsync(UpdatePluginDto updatePluginDto);
    Task<Guid> AddExistedPluginAsync(AddExistedPluginDto addExistedPluginDto);
    Task<IReadOnlyList<Assembly>> GetPluginAssembliesAsync(Guid tenantId);
    Task<IReadOnlyList<Assembly>> GetCurrentTenantPluginAssembliesAsync();
    /// <summary>
    /// Query plugin DLL load status for this startup.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <returns>Dictionary: key is DLL name, value is load status and error reason if failed</returns>
    Task<Dictionary<string, PluginLoadStatus>> GetPluginLoadStatusAsync(Guid? tenantId = null);
}