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
}