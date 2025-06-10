using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using MongoDB.Driver;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;
using Microsoft.Extensions.Logging;

namespace Aevatar.Plugins.Repositories;

public class PluginCodeStorageRepository :
    MongoDbRepository<PluginCodeStorageMongoDbContext, PluginCodeStorageSnapshotDocument, string>,
    IPluginCodeStorageRepository, ITransientDependency
{
    private string GAgentTypeName = typeof(PluginCodeStorageGAgent).FullName!;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<PluginCodeStorageRepository> _logger;

    public PluginCodeStorageRepository(
        IMongoDbContextProvider<PluginCodeStorageMongoDbContext> dbContextProvider,
        IServiceProvider serviceProvider,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<PluginCodeStorageRepository> logger) : base(dbContextProvider)
    {
        LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider);
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task<byte[]?> GetPluginCodeByGAgentPrimaryKey(Guid primaryKey)
    {
        var dbContext = await GetDbContextAsync();
        var document = await dbContext.PluginCodeStorage
            .Find(pc => pc.Id == $"{GAgentTypeName}/{primaryKey:N}")
            .ToListAsync();
        return document.FirstOrDefault()?.Doc.Snapshot.Code.Value;
    }

    public async Task<Dictionary<Type, string>> GetPluginDescriptionsByGAgentPrimaryKey(Guid primaryKey)
    {
        using var uow = _unitOfWorkManager.Begin();
        var dbContext = await GetDbContextAsync();
        var document = await dbContext.PluginCodeStorage
            .Find(pc => pc.Id == $"{GAgentTypeName}/{primaryKey:N}")
            .ToListAsync();
        await uow.CompleteAsync();
        var dict = document.FirstOrDefault()?.Doc.Snapshot.Descriptions ?? new Dictionary<string, string>();
        var result = new Dictionary<Type, string>();
        foreach (var kvp in dict.Skip(2))
        {
            var type = Type.GetType(kvp.Key);
            if (type != null)
            {
                result[type] = kvp.Value;
            }
            else
            {
                _logger?.LogWarning($"Could not resolve type from key: {kvp.Key}");
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<byte[]>> GetPluginCodesByGAgentPrimaryKeys(IReadOnlyList<Guid> primaryKeys)
    {
        using var uow = _unitOfWorkManager.Begin();
        var codeList = new List<byte[]>();
        foreach (var primaryKey in primaryKeys)
        {
            var code = await GetPluginCodeByGAgentPrimaryKey(primaryKey);
            if (code != null)
            {
                codeList.Add(code);
            }
        }

        await uow.CompleteAsync();
        return codeList;
    }

    protected override CancellationToken GetCancellationToken(
        CancellationToken preferredValue = new())
    {
        return new CancellationToken();
    }
}