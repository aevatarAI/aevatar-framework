using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins.GAgents;
using Aevatar.Plugins.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aevatar.Plugins;

public class PluginGAgentManager : IPluginGAgentManager
{
    protected readonly ILogger<PluginGAgentManager> Logger;

    private readonly IGAgentFactory _gAgentFactory;
    private readonly ITenantPluginCodeRepository _tenantPluginCodeRepository;
    private readonly IPluginCodeStorageRepository _pluginCodeStorageRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly PluginGAgentLoadOptions _options;

    public PluginGAgentManager(IGAgentFactory gAgentFactory,
        ITenantPluginCodeRepository tenantPluginCodeRepository,
        IPluginCodeStorageRepository pluginCodeStorageRepository,
        IOptions<PluginGAgentLoadOptions> options, ILogger<PluginGAgentManager> logger,
        IServiceProvider serviceProvider)
    {
        _gAgentFactory = gAgentFactory;
        _tenantPluginCodeRepository = tenantPluginCodeRepository;
        _pluginCodeStorageRepository = pluginCodeStorageRepository;
        Logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public async Task<Guid> AddPluginAsync(AddPluginDto addPluginDto)
    {
        if (addPluginDto.Code.Length == 0)
        {
            return Guid.Empty;
        }

        var pluginCodeGAgent = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(
            configuration: new PluginCodeStorageConfiguration
            {
                Code = addPluginDto.Code
            });
        var pluginCodeId = pluginCodeGAgent.GetPrimaryKey();
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(addPluginDto.TenantId);
        Logger.LogInformation($"About to plugin to tenant {addPluginDto.TenantId}.");
        await tenant.AddPluginAsync(pluginCodeId);
        Logger.LogInformation($"Added plugin to tenant {addPluginDto.TenantId}.");
        return pluginCodeId;
    }

    public async Task<IReadOnlyList<Guid>> GetPluginsAsync(Guid tenantId)
    {
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        var tenantState = await tenant.GetStateAsync();
        if (tenantState.CodeStorageGuids.IsNullOrEmpty()) return [];
        return tenantState.CodeStorageGuids;
    }

    public async Task<PluginsInformation> GetPluginsWithDescriptionAsync(Guid tenantId)
    {
        var pluginCodeIds = await GetPluginsAsync(tenantId);
        var pluginsInformation = new PluginsInformation();
        foreach (var pluginCodeId in pluginCodeIds)
        {
            var description = await GetPluginDescription(pluginCodeId);
            pluginsInformation.Value[pluginCodeId] = description;
        }

        return pluginsInformation;
    }

    public async Task<string> GetPluginDescription(Guid pluginCodeId)
    {
        var pluginCodeStorage =
            await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(pluginCodeId);
        return await pluginCodeStorage.GetDescriptionAsync();
    }

    public async Task RemovePluginAsync(RemovePluginDto removePluginDto)
    {
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(removePluginDto.TenantId);
        await tenant.RemovePluginAsync(removePluginDto.PluginCodeId);
    }

    public async Task UpdatePluginAsync(UpdatePluginDto updatePluginDto)
    {
        var pluginCodeStorageGAgent =
            await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(updatePluginDto.PluginCodeId);
        await pluginCodeStorageGAgent.UpdatePluginCodeAsync(updatePluginDto.Code);
    }

    public async Task<Guid> AddExistedPluginAsync(AddExistedPluginDto addExistedPluginDto)
    {
        var existedPluginCode =
            await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(addExistedPluginDto.PluginCodeId);
        var code = await existedPluginCode.GetPluginCodeAsync();
        if (code == null || code.Length == 0)
        {
            return Guid.Empty;
        }

        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(addExistedPluginDto.TenantId);
        var pluginCodeGAgent = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(
            configuration: new PluginCodeStorageConfiguration
            {
                Code = code
            });
        var pluginCodeId = pluginCodeGAgent.GetPrimaryKey();
        await tenant.AddPluginAsync(pluginCodeId);
        return pluginCodeId;
    }

    public async Task<IReadOnlyList<Assembly>> GetPluginAssembliesAsync(Guid tenantId)
    {
        var assemblies = new List<Assembly>();
        var pluginCodeGAgentPrimaryKeys =
            await _tenantPluginCodeRepository.GetGAgentPrimaryKeysByTenantIdAsync(tenantId);
        if (pluginCodeGAgentPrimaryKeys == null) return assemblies;
        var pluginCodes =
            await _pluginCodeStorageRepository.GetPluginCodesByGAgentPrimaryKeys(pluginCodeGAgentPrimaryKeys);
        assemblies = pluginCodes.Select(Assembly.Load).DistinctBy(assembly => assembly.FullName).ToList();
        return assemblies;
    }
}