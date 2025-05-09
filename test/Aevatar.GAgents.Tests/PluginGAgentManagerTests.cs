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

namespace Aevatar.GAgents.Tests;

[Collection("PluginTests")]
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

    private static readonly string TestPluginPath = "Plugins/Aevatar.GAgents.Plugins.dll";
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static byte[]? _pluginBytes;

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

    public async Task InitializeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_pluginBytes == null)
            {
                _pluginBytes = await File.ReadAllBytesAsync(TestPluginPath);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        (_tenantId, _pluginId) = await AddTestPluginAsync();
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

    private async Task<(Guid tenantId, Guid pluginId)> AddTestPluginAsync()
    {
        var tenantId = Guid.NewGuid();
        var addPluginDto = new AddPluginDto { TenantId = tenantId, Code = _pluginBytes! };
        var pluginId = await _pluginGAgentManager.AddPluginAsync(addPluginDto);
        _createdPlugins.Add((tenantId, pluginId));
        return (tenantId, pluginId);
    }

    [Fact(Skip = "Need mongodb.")]
    async Task MongoRepositoryTest()
    {
        // need to supply
        using (_unitOfWorkManager.Begin())
        {
            var primaryKey = Guid.NewGuid();
            var result = await _pluginCodeStorageRepository.GetPluginCodeByGAgentPrimaryKey(primaryKey);
            result.ShouldBeNull();

            var primaryKeyList = new List<Guid>() { primaryKey};
            var resultList = await _pluginCodeStorageRepository.GetPluginCodesByGAgentPrimaryKeys(primaryKeyList);
            resultList.Count.ShouldBe(0);
            
            var result1 = await _tenantPluginCodeRepository.GetGAgentPrimaryKeysByTenantIdAsync(primaryKey);
            result1.ShouldBeNull();
        }

        var tenantDoc = new TenantPluginCodeSnapshotDocument()
        {
            Etag = "",
            Doc = new TenantPluginCodeDocEntity()
            {
                InternalId = "",
                Type = "",
                Snapshot = new TenantPluginCodeSnapshotEntity()
                {
                    InternalId = "",
                    Type = "",
                    CodeStorageGuids = new CodeStorageGuidList()
                    {
                        Type = "",
                        Values = new List<Guid>()
                    }
                }
            }
        };
        tenantDoc.ToString();
        var pluginDoc = new PluginCodeStorageSnapshotDocument()
        {
            Etag = "",
            Doc = new PluginCodeStorageDoc()
            {
                InternalId = "",
                Type = "",
                Snapshot = new PluginCodeStorageSnapshot()
                {
                    InternalId = "",
                    Type = "",
                    Code = new ByteArrayContainer()
                    {
                        Type = "",
                        Value = new byte[1]
                    }
                }
            }
        };
    }

    [Fact(DisplayName = "Can add a new plugin successfully with real DLL")]
    public async Task AddPluginWithRealDllTest()
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

    [Fact(DisplayName = "Can get plugin description from real DLL")]
    public async Task GetPluginDescriptionWithRealDllTest()
    {
        await SyncStoreAsync(_tenantId, _pluginId);
        var description = await _pluginGAgentManager.GetPluginDescriptions(_pluginId);
        description.ShouldNotBeEmpty();
        description.Keys.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Can get plugin assemblies for a tenant with real DLL")]
    public async Task GetPluginAssembliesWithRealDllTest()
    {
        await SyncStoreAsync(_tenantId, _pluginId);
        var assemblies = await _pluginGAgentManager.GetPluginAssembliesAsync(_tenantId);
        assemblies.ShouldNotBeNull();
        assemblies.Count.ShouldBeGreaterThan(0);
        assemblies.Any(a => a.GetTypes().Any()).ShouldBeTrue();
    }

    [Fact(DisplayName = "Returns empty list when tenant has no plugin assemblies")]
    public async Task GetPluginAssembliesForEmptyTenantWithRealDllTest()
    {
        var tenantId = Guid.NewGuid();
        var assemblies = await _pluginGAgentManager.GetPluginAssembliesAsync(tenantId);
        assemblies.ShouldNotBeNull();
        assemblies.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Can get plugins for a tenant with real DLL")]
    public async Task GetPluginsWithRealDllTest()
    {
        var result = await _pluginGAgentManager.GetPluginsAsync(_tenantId);
        result.ShouldContain(_pluginId);
        result.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Can get plugins with descriptions for a tenant with real DLL")]
    public async Task GetPluginsWithDescriptionWithRealDllTest()
    {
        await SyncStoreAsync(_tenantId, _pluginId);
        var result = await _pluginGAgentManager.GetPluginsWithDescriptionAsync(_tenantId);
        result.Value.ShouldContainKey(_pluginId);
        result.Value.First().Value.Count.ShouldBe(1);
        result.Value.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Can add an existing plugin to a tenant with real DLL")]
    public async Task AddExistedPluginWithRealDllTest()
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
        await SyncStoreAsync(_tenantId, _pluginId);
        // Simulate a successful load status
        var statusDict = new Dictionary<string, PluginLoadStatus>
        {
            { $"Plugin_{_pluginId}.dll", new PluginLoadStatus { Status = LoadStatus.Success } }
        };
        await _pluginLoadStatusRepository.SetPluginLoadStatusAsync(_tenantId, statusDict);
        var result = await _pluginGAgentManager.GetPluginLoadStatusAsync(_tenantId);
        result.ShouldContainKey($"Plugin_{_pluginId}.dll");
        result[$"Plugin_{_pluginId}.dll"].Status.ShouldBe(LoadStatus.Success);
    }

    [Fact(DisplayName = "Can get plugin load status for partial failure case")]
    public async Task GetPluginLoadStatus_PartialFailure_Test()
    {
        await SyncStoreAsync(_tenantId, _pluginId);
        // Simulate a failed load status
        var statusDict = new Dictionary<string, PluginLoadStatus>
        {
            { $"Plugin_{_pluginId}.dll", new PluginLoadStatus { Status = LoadStatus.Error, Reason = "Test failure" } }
        };
        await _pluginLoadStatusRepository.SetPluginLoadStatusAsync(_tenantId, statusDict);
        var result = await _pluginGAgentManager.GetPluginLoadStatusAsync(_tenantId);
        result.ShouldContainKey($"Plugin_{_pluginId}.dll");
        result[$"Plugin_{_pluginId}.dll"].Status.ShouldBe(LoadStatus.Error);
        result[$"Plugin_{_pluginId}.dll"].Reason.ShouldBe("Test failure");
    }

    [Fact(DisplayName = "Returns empty dictionary when tenant has no plugins")]
    public async Task GetPluginLoadStatus_EmptyTenant_Test()
    {
        var tenantId = Guid.NewGuid();
        var result = await _pluginGAgentManager.GetPluginLoadStatusAsync(tenantId);
        result.ShouldBeEmpty();
    }
}