using System.Text;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins;
using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using Aevatar.Plugins.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace Aevatar.GAgents.Tests;

public class PluginGAgentManagerTests : AevatarGAgentsTestBase
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IPluginGAgentManager _pluginGAgentManager;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly Mock<ITenantPluginCodeRepository> _tenantPluginCodeRepositoryMock;
    private readonly Mock<IPluginCodeStorageRepository> _pluginCodeStorageRepositoryMock;
    private readonly Mock<ILogger<PluginGAgentManager>> _loggerMock;

    private readonly PluginCodeStorageMongoDbContext _pluginCodeStorageMongoDbContext;
    private readonly TenantPluginCodeMongoDbContext _tenantPluginCodeMongoDbContext;
    private readonly ITenantPluginCodeRepository _tenantPluginCodeRepository;
    private readonly IPluginCodeStorageRepository _pluginCodeStorageRepository;
    
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public PluginGAgentManagerTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
        _tenantPluginCodeRepositoryMock = new Mock<ITenantPluginCodeRepository>();
        _pluginCodeStorageRepositoryMock = new Mock<IPluginCodeStorageRepository>();
        _loggerMock = new Mock<ILogger<PluginGAgentManager>>();

        var options = Options.Create(new PluginGAgentLoadOptions());
        _pluginGAgentManager = new PluginGAgentManager(
            _gAgentFactory,
            _tenantPluginCodeRepositoryMock.Object,
            _pluginCodeStorageRepositoryMock.Object,
            options,
            _loggerMock.Object,
            GetRequiredService<IServiceProvider>()
        );
        _pluginCodeStorageMongoDbContext = GetRequiredService<PluginCodeStorageMongoDbContext>();
        _tenantPluginCodeMongoDbContext = GetRequiredService<TenantPluginCodeMongoDbContext>();
        _pluginCodeStorageRepository = GetRequiredService<IPluginCodeStorageRepository>();
        _tenantPluginCodeRepository = GetRequiredService<ITenantPluginCodeRepository>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact(Skip = "Need mongodb.")]
    async Task MongoRepositoryTest()
    {
        // need to supply
        using (var uow = _unitOfWorkManager.Begin())
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

    [Fact]
    public async Task LoadPluginTest()
    {
        var ss = PluginLoader.LoadPlugins("plugins");
        var tenantId = "test".ToGuid();
        foreach (var code in ss)
        {
            await _pluginGAgentManager.AddPluginAsync(new AddPluginDto
            {
                Code = code,
                TenantId = tenantId
            });
        }
    }

    [Fact(DisplayName = "Can add a new plugin successfully")]
    public async Task AddPluginTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code = "test plugin code";
        var addPluginDto = new AddPluginDto { TenantId = tenantId, Code = Encoding.UTF8.GetBytes(code) };

        // Act
        var result = await _pluginGAgentManager.AddPluginAsync(addPluginDto);

        // Assert
        result.ShouldNotBe(Guid.Empty);
        
        // Verify the plugin was added
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        var tenantState = await tenant.GetStateAsync();
        tenantState.CodeStorageGuids.ShouldContain(result);
        
        // Verify the plugin code was stored
        var pluginCodeStorage = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(result);
        var storedCode = await pluginCodeStorage.GetPluginCodeAsync();
        Encoding.UTF8.GetString(storedCode).ShouldBe(code);
    }

    [Fact(DisplayName = "Returns empty GUID when adding plugin with empty code")]
    public async Task AddPluginWithEmptyCodeTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var addPluginDto = new AddPluginDto { TenantId = tenantId, Code = Array.Empty<byte>() };

        // Act
        var result = await _pluginGAgentManager.AddPluginAsync(addPluginDto);

        // Assert
        result.ShouldBe(Guid.Empty);
    }

    [Fact(DisplayName = "Can get plugins for a tenant")]
    public async Task GetPluginsTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code1 = "test plugin code 1";
        var code2 = "test plugin code 2";
        
        // Add two plugins
        var plugin1Id = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code1) 
        });
        
        var plugin2Id = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code2) 
        });

        // Act
        var result = await _pluginGAgentManager.GetPluginsAsync(tenantId);

        // Assert
        result.ShouldContain(plugin1Id);
        result.ShouldContain(plugin2Id);
        result.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "Returns empty list when tenant has no plugins")]
    public async Task GetPluginsForEmptyTenantTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _pluginGAgentManager.GetPluginsAsync(tenantId);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Can remove a plugin from a tenant")]
    public async Task RemovePluginTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code = "test plugin code";
        
        // Add a plugin first
        var pluginId = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code) 
        });

        var removePluginDto = new RemovePluginDto { TenantId = tenantId, PluginCodeId = pluginId };

        // Act
        await _pluginGAgentManager.RemovePluginAsync(removePluginDto);

        // Assert
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        var tenantState = await tenant.GetStateAsync();
        tenantState.CodeStorageGuids.ShouldNotContain(pluginId);
    }

    [Fact(DisplayName = "Can update a plugin's code")]
    public async Task UpdatePluginTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var originalCode = "original plugin code";
        var newCode = "updated plugin code";
        
        // Add a plugin first
        var pluginId = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(originalCode) 
        });

        var updatePluginDto = new UpdatePluginDto 
        { 
            TenantId = tenantId, 
            PluginCodeId = pluginId, 
            Code = Encoding.UTF8.GetBytes(newCode) 
        };

        // Act
        await _pluginGAgentManager.UpdatePluginAsync(updatePluginDto);

        // Assert
        var pluginCodeStorage = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(pluginId);
        var storedCode = await pluginCodeStorage.GetPluginCodeAsync();
        Encoding.UTF8.GetString(storedCode).ShouldBe(newCode);
    }

    [Fact(DisplayName = "Can get plugins with descriptions for a tenant")]
    public async Task GetPluginsWithDescriptionTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code1 = "test plugin code 1";
        var code2 = "test plugin code 2";
        
        // Add two plugins
        var plugin1Id = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code1) 
        });
        
        var plugin2Id = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code2) 
        });

        // Act
        var result = await _pluginGAgentManager.GetPluginsWithDescriptionAsync(tenantId);

        // Assert
        result.Value.ShouldContainKey(plugin1Id);
        result.Value.ShouldContainKey(plugin2Id);
        result.Value.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "Can get plugin description")]
    public async Task GetPluginDescriptionTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code = "test plugin code";
        
        // Add a plugin
        var pluginId = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code) 
        });

        // Act
        var description = await _pluginGAgentManager.GetPluginDescription(pluginId);

        // Assert
        description.ShouldNotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Can add an existing plugin to a tenant")]
    public async Task AddExistedPluginTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code = "test plugin code";
        
        // Add original plugin
        var originalPluginId = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = Guid.NewGuid(), // Different tenant
            Code = Encoding.UTF8.GetBytes(code) 
        });

        var addExistedPluginDto = new AddExistedPluginDto 
        { 
            TenantId = tenantId, 
            PluginCodeId = originalPluginId 
        };

        // Act
        var newPluginId = await _pluginGAgentManager.AddExistedPluginAsync(addExistedPluginDto);

        // Assert
        newPluginId.ShouldNotBe(Guid.Empty);
        newPluginId.ShouldNotBe(originalPluginId); // Should create a new copy
        
        // Verify the plugin was added to the new tenant
        var tenant = await _gAgentFactory.GetGAgentAsync<ITenantPluginCodeGAgent>(tenantId);
        var tenantState = await tenant.GetStateAsync();
        tenantState.CodeStorageGuids.ShouldContain(newPluginId);
        
        // Verify the plugin code was copied
        var pluginCodeStorage = await _gAgentFactory.GetGAgentAsync<IPluginCodeStorageGAgent>(newPluginId);
        var storedCode = await pluginCodeStorage.GetPluginCodeAsync();
        Encoding.UTF8.GetString(storedCode).ShouldBe(code);
    }

    [Fact(DisplayName = "Returns empty GUID when adding existing plugin with empty code")]
    public async Task AddExistedPluginWithEmptyCodeTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var originalPluginId = Guid.NewGuid();
        
        // Mock the repository to return empty code
        _pluginCodeStorageRepositoryMock.Setup(x => x.GetPluginCodesByGAgentPrimaryKeys(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<byte[]> { Array.Empty<byte>() });

        var addExistedPluginDto = new AddExistedPluginDto 
        { 
            TenantId = tenantId, 
            PluginCodeId = originalPluginId 
        };

        // Act
        var result = await _pluginGAgentManager.AddExistedPluginAsync(addExistedPluginDto);

        // Assert
        result.ShouldBe(Guid.Empty);
    }

    [Fact(DisplayName = "Can get plugin assemblies for a tenant")]
    public async Task GetPluginAssembliesTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code = "test plugin code";
        
        // Add a plugin
        var pluginId = await _pluginGAgentManager.AddPluginAsync(new AddPluginDto 
        { 
            TenantId = tenantId, 
            Code = Encoding.UTF8.GetBytes(code) 
        });

        // Mock the repository to return the plugin code
        _tenantPluginCodeRepositoryMock.Setup(x => x.GetGAgentPrimaryKeysByTenantIdAsync(tenantId))
            .ReturnsAsync(new List<Guid> { pluginId });
        _pluginCodeStorageRepositoryMock.Setup(x => x.GetPluginCodesByGAgentPrimaryKeys(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<byte[]> { Encoding.UTF8.GetBytes(code) });

        await Assert.ThrowsAsync<BadImageFormatException>(() => _pluginGAgentManager.GetPluginAssembliesAsync(tenantId));
    }

    [Fact(DisplayName = "Returns empty list when tenant has no plugin assemblies")]
    public async Task GetPluginAssembliesForEmptyTenantTest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Mock the repository to return null
        _tenantPluginCodeRepositoryMock.Setup(x => x.GetGAgentPrimaryKeysByTenantIdAsync(tenantId))
            .ReturnsAsync((List<Guid>)null);

        // Act
        var assemblies = await _pluginGAgentManager.GetPluginAssembliesAsync(tenantId);

        // Assert
        assemblies.ShouldNotBeNull();
        assemblies.ShouldBeEmpty();
    }
}