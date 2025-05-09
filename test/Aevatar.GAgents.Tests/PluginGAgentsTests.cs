namespace Aevatar.GAgents.Tests;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Plugin;
using Aevatar.Plugins.GAgents;
using Plugins.Repositories;
using Shouldly;
using Xunit;

[Collection(TestBase.ClusterCollection.Name)]
public class PluginGAgentsTests : AevatarGAgentsTestBase
{
    public PluginGAgentsTests()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var assemblies = new List<Assembly>();
            foreach (var file in Directory.EnumerateFiles("Plugins", "*.dll"))
            {
                var bytes = File.ReadAllBytes(file);
                assemblies.Add(Assembly.Load(bytes));
            }
            var assemblyName = new AssemblyName(args.Name).Name;
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);
            return assembly != null ? assembly : null;
        };
    }
    
    [Fact(DisplayName = "All-in-one Plugin DLL Test.")]
    public async Task PluginDllIntegrationTest()
    {
        var gAgentFactory = GetRequiredService<IGAgentFactory>();
        var pluginGAgentManager = GetRequiredService<IPluginGAgentManager>();
        var tenantPluginCodeRepository = GetRequiredService<ITenantPluginCodeRepository>();
        var pluginCodeStorageRepository = GetRequiredService<IPluginCodeStorageRepository>();
        var pluginLoadStatusRepository = GetRequiredService<IPluginLoadStatusRepository>();

        var pluginPath = "Plugins/Aevatar.GAgents.Plugins.dll";
        var pluginBytes = await File.ReadAllBytesAsync(pluginPath);
        var tenantId = Guid.NewGuid();
        var addPluginDto = new AddPluginDto { TenantId = tenantId, Code = pluginBytes };
        var pluginId = await pluginGAgentManager.AddPluginAsync(addPluginDto);

        // Assert plugin code has been added.
        var tenant = await gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        var tenantState = await tenant.GetStateAsync();
        tenantState.CodeStorageGuids.ShouldContain(pluginId);
        var pluginCodeStorage = await gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(pluginId);
        var storedCode = await pluginCodeStorage.GetPluginCodeAsync();
        storedCode.ShouldNotBeNull();
        storedCode.Length.ShouldBeGreaterThan(0);

        // Sync grain state to repositories.
        await ((InMemoryTenantPluginCodeRepository)tenantPluginCodeRepository).SyncStoreAsync(tenant);
        await ((InMemoryPluginCodeStorageRepository)pluginCodeStorageRepository).SyncStoreAsync(pluginCodeStorage);

        // Able to get descriptions.
        var description = await pluginGAgentManager.GetPluginDescriptions(pluginId);
        description.ShouldNotBeEmpty();
        description.Keys.Count.ShouldBe(1);

        // Able to get assemblies.
        var assemblies = await pluginGAgentManager.GetPluginAssembliesAsync(tenantId);
        assemblies.ShouldNotBeNull();
        assemblies.Count.ShouldBeGreaterThan(0);
        assemblies.Any(a => a.GetTypes().Any()).ShouldBeTrue();

        // Able to get plugin list.
        var plugins = await pluginGAgentManager.GetPluginsAsync(tenantId);
        plugins.ShouldContain(pluginId);
        plugins.Count.ShouldBe(1);

        // Able to get description list.
        var pluginsWithDesc = await pluginGAgentManager.GetPluginsWithDescriptionAsync(tenantId);
        pluginsWithDesc.Value.ShouldContainKey(pluginId);
        pluginsWithDesc.Value.First().Value.Count.ShouldBe(1);
        pluginsWithDesc.Value.Count.ShouldBe(1);

        // Able to get load status.
        var statusDict = new Dictionary<string, PluginLoadStatus>
        {
            { $"Plugin_{pluginId}.dll", new PluginLoadStatus { Status = LoadStatus.Success } }
        };
        await pluginLoadStatusRepository.SetPluginLoadStatusAsync(tenantId, statusDict);
        var result = await pluginGAgentManager.GetPluginLoadStatusAsync(tenantId);
        result.ShouldContainKey($"Plugin_{pluginId}.dll");
        result[$"Plugin_{pluginId}.dll"].Status.ShouldBe(LoadStatus.Success);
    }
}