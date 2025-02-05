using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.EventSourcing.Core.Snapshot;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Storage;

namespace Aevatar.Plugins;

public class PluginGAgentManager : IPluginGAgentManager, ILifecycleParticipant<ISiloLifecycle>
{
    protected readonly ILogger<PluginGAgentManager> Logger;

    private readonly ApplicationPartManager _applicationPartManager;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly PluginGAgentLoadOptions _options;

    public PluginGAgentManager(ApplicationPartManager applicationPartManager, IGAgentFactory gAgentFactory,
        IOptions<PluginGAgentLoadOptions> options, ILogger<PluginGAgentManager> logger,
        IServiceProvider serviceProvider)
    {
        _applicationPartManager = applicationPartManager;
        _gAgentFactory = gAgentFactory;
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

    public async Task<List<Assembly>> GetPluginAssembliesAsync(Guid tenantId)
    {
        var assemblies = new List<Assembly>();
        var grainStorage = _serviceProvider.GetRequiredKeyedService<IGrainStorage>("PubSubStore");
        var tenantGrainState = new GrainState<ViewStateSnapshotWithMetadata<TenantPluginCodeGAgentState>>();
        var tenantGrainId = GrainId.Create("Aevatar.Plugins/pluginTenant", tenantId.ToString("N"));
        await grainStorage.ReadStateAsync(typeof(TenantPluginCodeGAgent).FullName, tenantGrainId, tenantGrainState);
        if (tenantGrainState.State == null)
        {
            return assemblies;
        }
        var tenantState = tenantGrainState.State.Snapshot;
        if (tenantState.CodeStorageGuids.IsNullOrEmpty()) return new List<Assembly>();

        foreach (var pluginCodeStorageGuid in tenantState.CodeStorageGuids)
        {
            var pluginCodeStorageGrainState =
                new GrainState<ViewStateSnapshotWithMetadata<PluginCodeStorageGAgentState>>();
            var codeGrainId = GrainId.Create("Aevatar.Plugins/pluginCodeStorage", pluginCodeStorageGuid.ToString("N"));
            await grainStorage.ReadStateAsync(typeof(PluginCodeStorageGAgent).FullName, codeGrainId,
                pluginCodeStorageGrainState);
            var code = pluginCodeStorageGrainState.State.Snapshot.Code;
            assemblies.Add(Assembly.Load(code));
        }

        return assemblies;
    }

    private async Task LoadPluginGAgentsAsync(Guid tenantId)
    {
        var assemblies = await GetPluginAssembliesAsync(tenantId);
        foreach (var assembly in assemblies)
        {
            _applicationPartManager.ApplicationParts.Add(new AssemblyPart(assembly));
            Logger.LogInformation("Loaded assembly: {Assembly}", assembly.FullName);
        }
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(
            nameof(PluginGAgentManager),
            ServiceLifecycleStage.First,
            OnStart
        );
    }

    private async Task OnStart(CancellationToken cancellationToken)
    {
        if (_options.TenantId == Guid.Empty) return;
        await LoadPluginGAgentsAsync(_options.TenantId);

        foreach (var part in _applicationPartManager.ApplicationParts.OfType<AssemblyPart>())
        {
            foreach (var type in part.Types)
            {
                if (typeof(IGAgent).IsAssignableFrom(type))
                {
                    Logger.LogInformation("Registered GAgent: {GrainType}", type.FullName);
                }
            }
        }
    }
}

public class TestManager
{
    private readonly IGrainStorage _grainStorage;

    public TestManager(IGrainStorage grainStorage)
    {
        _grainStorage = grainStorage;
    }

    public async Task<List<Assembly>> GetAssemblies(Guid tenantId)
    {
        var assemblies = new List<Assembly>();
        var tenantGrainState = new GrainState<ViewStateSnapshotWithMetadata<TenantPluginCodeGAgentState>>();
        var tenantGrainId = GrainId.Create("Aevatar.Plugins/pluginTenant", tenantId.ToString("N"));
        await _grainStorage.ReadStateAsync(typeof(TenantPluginCodeGAgent).FullName, tenantGrainId, tenantGrainState);
        var tenantState = tenantGrainState.State.Snapshot;
        if (tenantState.CodeStorageGuids.IsNullOrEmpty()) return new List<Assembly>();

        foreach (var pluginCodeStorageGuid in tenantState.CodeStorageGuids)
        {
            var pluginCodeStorageGrainState =
                new GrainState<ViewStateSnapshotWithMetadata<PluginCodeStorageGAgentState>>();
            var codeGrainId = GrainId.Create("Aevatar.Plugins/pluginCodeStorage", pluginCodeStorageGuid.ToString("N"));
            await _grainStorage.ReadStateAsync(typeof(PluginCodeStorageGAgent).FullName, codeGrainId,
                pluginCodeStorageGrainState);
            var code = pluginCodeStorageGrainState.State.Snapshot.Code;
            assemblies.Add(Assembly.Load(code));
        }

        return assemblies;
    }
}