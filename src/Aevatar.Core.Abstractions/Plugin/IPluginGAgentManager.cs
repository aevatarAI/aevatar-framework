using System.Reflection;
using Aevatar.Core.Abstractions.Plugin;

namespace Aevatar.Core.Abstractions.Plugin;

public interface IPluginGAgentManager
{
    Task<Guid> AddPluginAsync(AddPluginDto addPluginDto);
    Task<IReadOnlyList<Guid>> GetPluginsAsync(Guid tenantId);
    Task<PluginsInformation> GetPluginsWithDescriptionAsync(Guid tenantId);
    Task<string> GetPluginDescription(Guid pluginCodeId);
    Task RemovePluginAsync(RemovePluginDto removePluginDto);
    Task UpdatePluginAsync(UpdatePluginDto updatePluginDto);
    Task<Guid> AddExistedPluginAsync(AddExistedPluginDto addExistedPluginDto);
    Task<IReadOnlyList<Assembly>> GetPluginAssembliesAsync(Guid tenantId);
}