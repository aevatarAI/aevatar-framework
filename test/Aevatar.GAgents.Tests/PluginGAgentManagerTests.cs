using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using Aevatar.Plugins.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Volo.Abp.Uow;
using Xunit.Abstractions;
using System.Runtime.Loader;

namespace Aevatar.GAgents.Tests;

[Collection(TestBase.ClusterCollection.Name)]
public class PluginGAgentManagerTests : AevatarGAgentsTestBase, IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IPluginGAgentManager _pluginGAgentManager;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly Mock<ILogger<PluginGAgentManager>> _loggerMock;
    private readonly List<(Guid tenantId, Guid pluginId)> _createdPlugins = new();

    private readonly ITenantPluginCodeRepository _tenantPluginCodeRepository;
    private readonly IPluginCodeStorageRepository _pluginCodeStorageRepository;
    private readonly IPluginLoadStatusRepository _pluginLoadStatusRepository;
    
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    private static string TestPluginPath = "Plugins/Aevatar.GAgents.Plugins.dll";
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static byte[]? _pluginBytes;
    private static List<byte[]?> _pluginBytesList = [];
    private static Assembly? _pluginAssembly;

    private Guid _tenantId;
    private Guid _pluginId;

    public PluginGAgentManagerTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
        _tenantPluginCodeRepository = GetRequiredService<ITenantPluginCodeRepository>();
        _pluginCodeStorageRepository = GetRequiredService<IPluginCodeStorageRepository>();
        _pluginLoadStatusRepository = GetRequiredService<IPluginLoadStatusRepository>();
        _loggerMock = new Mock<ILogger<PluginGAgentManager>>();

        var options = Options.Create(new PluginGAgentLoadOptions());
        _pluginGAgentManager = new PluginGAgentManager(
            _gAgentFactory,
            _tenantPluginCodeRepository,
            _pluginCodeStorageRepository,
            _pluginLoadStatusRepository,
            options,
            _loggerMock.Object
        );

        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();

        if (_pluginAssembly == null)
        {
            var bytes = File.ReadAllBytes(TestPluginPath);
            _pluginAssembly = Assembly.Load(bytes);
        }
        AppDomain.CurrentDomain.AssemblyResolve -= PluginAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += PluginAssemblyResolve;
    }

    private static Assembly? PluginAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (_pluginAssembly != null && _pluginAssembly.GetName().Name == assemblyName)
        {
            return _pluginAssembly;
        }
        return null;
    }

    public async Task InitializeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_pluginBytes == null)
            {
                _pluginBytes = await File.ReadAllBytesAsync(TestPluginPath);
            }

            for (var i = 1; i < 6; i++)
            {
                var bytes = await File.ReadAllBytesAsync($"Plugins/Aevatar.GAgents.Plugins{i}.dll");
                _pluginBytesList.Add(bytes);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        if (_tenantId == Guid.Empty)
        {
            (_tenantId, _pluginId) = await AddTestPluginAsync();
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var (tenantId, pluginId) in _createdPlugins)
        {
            try
            {
                var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
                await tenant.RemovePluginAsync(pluginId);
                await SyncStoreAsync(tenantId, pluginId);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task<(Guid tenantId, Guid pluginId)> AddTestPluginAsync(int? index = null)
    {
        var tenantId = Guid.NewGuid();
        var bytes = index == null ? _pluginBytes : _pluginBytesList[index.Value];
        var addPluginDto = new AddPluginDto { TenantId = tenantId, Code = bytes! };
        var pluginId = await _pluginGAgentManager.AddPluginAsync(addPluginDto);
        _createdPlugins.Add((tenantId, pluginId));
        return (tenantId, pluginId);
    }

    [Fact(DisplayName = "Can add a new plugin successfully with real DLL (AssemblyLoadContext)")]
    public async Task AddPluginWithRealDllTest()
    {
        var pluginBytes = await File.ReadAllBytesAsync(TestPluginPath);
        var alc = new AssemblyLoadContext($"Plugin_{_pluginId}", isCollectible: true);
        Assembly? pluginAssembly = null;
        using (var ms = new MemoryStream(pluginBytes))
        {
            pluginAssembly = alc.LoadFromStream(ms);
        }
        try
        {
            _pluginId.ShouldNotBe(Guid.Empty);
            var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(_tenantId);
            var tenantState = await tenant.GetStateAsync();
            tenantState.CodeStorageGuids.ShouldContain(_pluginId);
            var pluginCodeStorage = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(_pluginId);
            var storedCode = await pluginCodeStorage.GetPluginCodeAsync();
            storedCode.ShouldNotBeNull();
            storedCode.Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact(DisplayName = "Can get plugin description from real DLL (AssemblyLoadContext)")]
    public async Task GetPluginDescriptionWithRealDllTest()
    {
        var (tenantId, pluginId) = await AddTestPluginAsync(1);
        var pluginBytes = _pluginBytesList[0];
        var alc = new AssemblyLoadContext($"Plugin_{pluginId}", isCollectible: true);
        Assembly? pluginAssembly = null;
        using (var ms = new MemoryStream(pluginBytes))
        {
            pluginAssembly = alc.LoadFromStream(ms);
        }
        try
        {
            await SyncStoreAsync(tenantId, pluginId);
            var description = await _pluginGAgentManager.GetPluginDescriptions(pluginId);
            description.ShouldNotBeEmpty();
            description.Keys.Count.ShouldBe(1);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact(DisplayName = "Can get plugin assemblies for a tenant with real DLL (AssemblyLoadContext)")]
    public async Task GetPluginAssembliesWithRealDllTest()
    {
        var (tenantId, pluginId) = await AddTestPluginAsync(2);
        var pluginBytes = _pluginBytesList[1];
        var alc = new AssemblyLoadContext($"Plugin_{pluginId}", isCollectible: true);
        Assembly? pluginAssembly = null;
        using (var ms = new MemoryStream(pluginBytes))
        {
            pluginAssembly = alc.LoadFromStream(ms);
        }
        try
        {
            await SyncStoreAsync(tenantId, pluginId);
            var assemblies = await _pluginGAgentManager.GetPluginAssembliesAsync(tenantId);
            assemblies.ShouldNotBeNull();
            assemblies.Count.ShouldBeGreaterThan(0);
            assemblies.Any(a => a.GetTypes().Any()).ShouldBeTrue();
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact(DisplayName = "Returns empty list when tenant has no plugin assemblies")]
    public async Task GetPluginAssembliesForEmptyTenantWithRealDllTest()
    {
        var tenantId = Guid.NewGuid();
        var assemblies = await _pluginGAgentManager.GetPluginAssembliesAsync(tenantId);
        assemblies.ShouldNotBeNull();
        assemblies.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Can get plugins for a tenant with real DLL (AssemblyLoadContext)")]
    public async Task GetPluginsWithRealDllTest()
    {
        var pluginBytes = await File.ReadAllBytesAsync(TestPluginPath);
        var alc = new AssemblyLoadContext($"Plugin_{_pluginId}", isCollectible: true);
        Assembly? pluginAssembly = null;
        using (var ms = new MemoryStream(pluginBytes))
        {
            pluginAssembly = alc.LoadFromStream(ms);
        }
        try
        {
            var result = await _pluginGAgentManager.GetPluginsAsync(_tenantId);
            result.ShouldContain(_pluginId);
            result.Count.ShouldBe(1);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact(DisplayName = "Can get plugins with descriptions for a tenant with real DLL (AssemblyLoadContext)")]
    public async Task GetPluginsWithDescriptionWithRealDllTest()
    {
        var (tenantId, pluginId) = await AddTestPluginAsync(2);
        var pluginBytes = _pluginBytesList[1];
        var alc = new AssemblyLoadContext($"Plugin_{pluginId}", isCollectible: true);
        Assembly? pluginAssembly = null;
        using (var ms = new MemoryStream(pluginBytes))
        {
            pluginAssembly = alc.LoadFromStream(ms);
        }
        try
        {
            await SyncStoreAsync(tenantId, pluginId);
            var result = await _pluginGAgentManager.GetPluginsWithDescriptionAsync(tenantId);
            result.Value.ShouldContainKey(pluginId);
            result.Value.First().Value.Count.ShouldBe(1);
            result.Value.Count.ShouldBe(1);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact(DisplayName = "Can add an existing plugin to a tenant with real DLL (AssemblyLoadContext)")]
    public async Task AddExistedPluginWithRealDllTest()
    {
        var pluginBytes = await File.ReadAllBytesAsync(TestPluginPath);
        var alc = new AssemblyLoadContext($"Plugin_{_pluginId}", isCollectible: true);
        Assembly? pluginAssembly = null;
        using (var ms = new MemoryStream(pluginBytes))
        {
            pluginAssembly = alc.LoadFromStream(ms);
        }
        try
        {
            var tenantId = Guid.NewGuid();
            var addExistedPluginDto = new AddExistedPluginDto
            {
                TenantId = tenantId,
                PluginCodeId = _pluginId
            };
            var newPluginId = await _pluginGAgentManager.AddExistedPluginAsync(addExistedPluginDto);
            _createdPlugins.Add((tenantId, newPluginId));
            newPluginId.ShouldNotBe(Guid.Empty);
            newPluginId.ShouldNotBe(_pluginId);
            var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
            var tenantState = await tenant.GetStateAsync();
            tenantState.CodeStorageGuids.ShouldContain(newPluginId);
            var pluginCodeStorage = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(newPluginId);
            var storedCode = await pluginCodeStorage.GetPluginCodeAsync();
            storedCode.ShouldNotBeNull();
            storedCode.Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            alc.Unload();
        }
    }

    private async Task SyncStoreAsync(Guid tenantId, Guid pluginId)
    {
        var tenantGAgent = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        await ((InMemoryTenantPluginCodeRepository)_tenantPluginCodeRepository).SyncStoreAsync(tenantGAgent);
        var pluginCodeStorageGAgent = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(pluginId);
        await ((InMemoryPluginCodeStorageRepository)_pluginCodeStorageRepository).SyncStoreAsync(pluginCodeStorageGAgent);
    }

    [Fact(DisplayName = "Can get plugin load status for all success case")]
    public async Task GetPluginLoadStatus_AllSuccess_Test()
    {
        var (tenantId, pluginId) = await AddTestPluginAsync(2);
        await SyncStoreAsync(tenantId, pluginId);
        // Simulate a successful load status
        var statusDict = new Dictionary<string, PluginLoadStatus>
        {
            { $"Plugin_{pluginId}.dll", new PluginLoadStatus { Status = LoadStatus.Success } }
        };
        await _pluginLoadStatusRepository.SetPluginLoadStatusAsync(tenantId, statusDict);
        var result = await _pluginGAgentManager.GetPluginLoadStatusAsync(tenantId);
        result.ShouldContainKey($"Plugin_{pluginId}.dll");
        result[$"Plugin_{pluginId}.dll"].Status.ShouldBe(LoadStatus.Success);
    }

    [Fact(DisplayName = "Can get plugin load status for partial failure case")]
    public async Task GetPluginLoadStatus_PartialFailure_Test()
    {
        var (tenantId, pluginId) = await AddTestPluginAsync(2);
        await SyncStoreAsync(tenantId, pluginId);
        // Simulate a failed load status
        var statusDict = new Dictionary<string, PluginLoadStatus>
        {
            { $"Plugin_{pluginId}.dll", new PluginLoadStatus { Status = LoadStatus.Error, Reason = "Test failure" } }
        };
        await _pluginLoadStatusRepository.SetPluginLoadStatusAsync(tenantId, statusDict);
        var result = await _pluginGAgentManager.GetPluginLoadStatusAsync(tenantId);
        result.ShouldContainKey($"Plugin_{pluginId}.dll");
        result[$"Plugin_{pluginId}.dll"].Status.ShouldBe(LoadStatus.Error);
        result[$"Plugin_{pluginId}.dll"].Reason.ShouldBe("Test failure");
    }

    [Fact(DisplayName = "Returns empty dictionary when tenant has no plugins")]
    public async Task GetPluginLoadStatus_EmptyTenant_Test()
    {
        var tenantId = Guid.NewGuid();
        var result = await _pluginGAgentManager.GetPluginLoadStatusAsync(tenantId);
        result.ShouldBeEmpty();
    }
}