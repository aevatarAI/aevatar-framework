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
    private readonly IPluginLoadStatusRepository _pluginLoadStatusRepository;
    private readonly PluginGAgentLoadOptions _pluginsOptions;

    public PluginGAgentManager(IGAgentFactory gAgentFactory,
        ITenantPluginCodeRepository tenantPluginCodeRepository,
        IPluginCodeStorageRepository pluginCodeStorageRepository,
        IPluginLoadStatusRepository pluginLoadStatusRepository,
        IOptions<PluginGAgentLoadOptions> options, ILogger<PluginGAgentManager> logger)
    {
        _gAgentFactory = gAgentFactory;
        _tenantPluginCodeRepository = tenantPluginCodeRepository;
        _pluginCodeStorageRepository = pluginCodeStorageRepository;
        _pluginLoadStatusRepository = pluginLoadStatusRepository;
        Logger = logger;
        _pluginsOptions = options.Value;
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
            var descriptions = await GetPluginDescriptions(pluginCodeId);
            pluginsInformation.Value[pluginCodeId] = descriptions;
        }

        return pluginsInformation;
    }

    public async Task<Dictionary<Type, string>> GetPluginDescriptions(Guid pluginCodeId)
    {
        var descriptions = await _pluginCodeStorageRepository.GetPluginDescriptionsByGAgentPrimaryKey(pluginCodeId);
        return descriptions;
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
        if (code.Length == 0)
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

    public async Task<IReadOnlyList<Assembly>> GetCurrentTenantPluginAssembliesAsync()
    {
        var tenantId = _pluginsOptions.TenantId;
        if (tenantId == Guid.Empty)
        {
            return [];
        }

        return await GetPluginAssembliesAsync(tenantId);
    }

    public async Task<Dictionary<string, PluginLoadStatus>> GetPluginLoadStatusAsync(Guid? tenantId = null)
    {
        tenantId ??= _pluginsOptions.TenantId;
        if (tenantId.Value == Guid.Empty)
        {
            return new Dictionary<string, PluginLoadStatus>();
        }

        var result = await _pluginLoadStatusRepository.GetPluginLoadStatusAsync(tenantId.Value);
        Logger.LogInformation(
            $"[GetPluginLoadStatusAsync] Loaded status for tenant: {tenantId}, count: {result.Count}");
        return result;
    }
}