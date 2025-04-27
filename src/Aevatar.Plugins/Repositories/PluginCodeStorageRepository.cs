using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using MongoDB.Driver;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;

namespace Aevatar.Plugins.Repositories;

public class PluginCodeStorageRepository :
    MongoDbRepository<PluginCodeStorageMongoDbContext, PluginCodeStorageSnapshotDocument, string>,
    IPluginCodeStorageRepository, ITransientDependency
{
    private string GAgentTypeName = typeof(PluginCodeStorageGAgent).FullName!;

    public PluginCodeStorageRepository(IMongoDbContextProvider<PluginCodeStorageMongoDbContext> dbContextProvider,
        IServiceProvider serviceProvider) : base(dbContextProvider)
    {
        LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider);
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
        using var uow = UnitOfWorkManager.Begin();
        var dbContext = await GetDbContextAsync();
        var document = await dbContext.PluginCodeStorage
            .Find(pc => pc.Id == $"{GAgentTypeName}/{primaryKey:N}")
            .ToListAsync();
        await uow.CompleteAsync();
        var dict = document.FirstOrDefault()?.Doc.Snapshot.Descriptions.ToDictionary(e => e.Key, e => e.Value) ??
                   new Dictionary<string, string>();
        return dict.Skip(2).ToDictionary(d => Type.GetType(d.Key), d => d.Value);
    }

    public async Task<IReadOnlyList<byte[]>> GetPluginCodesByGAgentPrimaryKeys(IReadOnlyList<Guid> primaryKeys)
    {
        var codeList = new List<byte[]>();
        foreach (var primaryKey in primaryKeys)
        {
            var code = await GetPluginCodeByGAgentPrimaryKey(primaryKey);
            if (code != null)
            {
                codeList.Add(code);
            }
        }

        return codeList;
    }

    protected override CancellationToken GetCancellationToken(
        CancellationToken preferredValue = new())
    {
        return new CancellationToken();
    }
}