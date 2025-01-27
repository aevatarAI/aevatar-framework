using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Plugin;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aevatar.Plugins;

public class PluginGAgentManager : IPluginGAgentManager, ILifecycleParticipant<ISiloLifecycle>
{
    protected readonly ILogger<PluginGAgentManager> Logger;

    private readonly ApplicationPartManager _applicationPartManager;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly PluginGAgentLoadOptions _options;

    public PluginGAgentManager(ApplicationPartManager applicationPartManager, IGAgentFactory gAgentFactory,
        IOptions<PluginGAgentLoadOptions> options, ILogger<PluginGAgentManager> logger)
    {
        _applicationPartManager = applicationPartManager;
        _gAgentFactory = gAgentFactory;
        Logger = logger;
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

    public async Task<List<Guid>> GetPluginsAsync(Guid tenantId)
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

    public async Task LoadPluginGAgentsAsync(Guid tenantId)
    {
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        var tenantState = await tenant.GetStateAsync();
        if (tenantState.CodeStorageGuids.IsNullOrEmpty()) return;
        var pluginCodeStorageGuids = tenantState.CodeStorageGuids;
        foreach (var pluginCodeStorageGuid in pluginCodeStorageGuids)
        {
            var pluginCodeStorage =
                await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(pluginCodeStorageGuid);
            var code = await pluginCodeStorage.GetPluginCodeAsync();
            var assembly = Assembly.Load(code);
            _applicationPartManager.ApplicationParts.Add(new AssemblyPart(assembly));
        }
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(nameof(PluginGAgentManager), ServiceLifecycleStage.ApplicationServices, OnStart);
    }

    private async Task OnStart(CancellationToken cancellationToken)
    {
        await LoadPluginGAgentsAsync(_options.TenantId);
    }
}