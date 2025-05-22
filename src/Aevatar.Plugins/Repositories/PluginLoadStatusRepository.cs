using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;

namespace Aevatar.Plugins.Repositories;

public class PluginLoadStatusRepository :
    MongoDbRepository<PluginLoadStatusMongoDbContext, PluginLoadStatusDocument, string>,
    IPluginLoadStatusRepository, ITransientDependency
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<PluginLoadStatusRepository> _logger;

    public PluginLoadStatusRepository(
        IMongoDbContextProvider<PluginLoadStatusMongoDbContext> dbContextProvider,
        IServiceProvider serviceProvider,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<PluginLoadStatusRepository> logger) : base(dbContextProvider)
    {
        LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider);
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task<Dictionary<string, PluginLoadStatus>> GetPluginLoadStatusAsync(Guid tenantId)
    {
        using var uow = _unitOfWorkManager.Begin();
        var dbContext = await GetDbContextAsync();
        var document = await dbContext.PluginLoadStatus
            .Find(pc => pc.TenantId == tenantId)
            .ToListAsync();
        await uow.CompleteAsync();
        var status = document.FirstOrDefault()?.LoadStatus;
        return status ?? new Dictionary<string, PluginLoadStatus>();
    }

    public async Task SetPluginLoadStatusAsync(Guid tenantId,
        Dictionary<string, PluginLoadStatus> status)
    {
        var dbContext = await GetDbContextAsync();
        var document = await dbContext.PluginLoadStatus.Find(pl => pl.TenantId == tenantId).FirstOrDefaultAsync();

        if (document == null)
        {
            await dbContext.PluginLoadStatus.InsertOneAsync(new PluginLoadStatusDocument
            {
                TenantId = tenantId,
                LoadStatus = status
            });
            _logger?.LogInformation($"[SetPluginLoadStatus] Insert load status for tenant: {tenantId}");
            return;
        }

        document.LoadStatus = status;
        await dbContext.PluginLoadStatus.ReplaceOneAsync(pc => pc.TenantId == tenantId, document);
        _logger?.LogInformation($"[SetPluginLoadStatus] Updated load status for tenant: {tenantId}");
    }

    public async Task ClearPluginLoadStatusAsync()
    {
        var dbContext = await GetDbContextAsync();
        await dbContext.PluginLoadStatus.DeleteManyAsync(FilterDefinition<PluginLoadStatusDocument>.Empty);
        _logger?.LogInformation("[ClearPluginLoadStatus] Cleared all plugin load statuses.");
    }
}