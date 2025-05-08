using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using MongoDB.Driver;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;

namespace Aevatar.Plugins.Repositories;

public class TenantPluginCodeRepository :
    MongoDbRepository<TenantPluginCodeMongoDbContext, TenantPluginCodeSnapshotDocument, string>,
    ITenantPluginCodeRepository, ITransientDependency
{
    private string GAgentTypeName = typeof(TenantPluginCodeGAgent).FullName!;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public TenantPluginCodeRepository(IMongoDbContextProvider<TenantPluginCodeMongoDbContext> dbContextProvider,
        IServiceProvider serviceProvider, IUnitOfWorkManager unitOfWorkManager) : base(dbContextProvider)
    {
        LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider);
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<IReadOnlyList<Guid>?> GetGAgentPrimaryKeysByTenantIdAsync(Guid tenantId)
    {
        using var uow = _unitOfWorkManager.Begin();
        var dbContext = await GetDbContextAsync();
        var grainIdString = $"{GAgentTypeName}/{tenantId:N}";
        var document = await dbContext.TenantPluginCode
            .Find(pc => pc.Id == grainIdString)
            .ToListAsync();
        await uow.CompleteAsync();
        return document.FirstOrDefault()?.Doc.Snapshot.CodeStorageGuids.Values;
    }

    protected override CancellationToken GetCancellationToken(
        CancellationToken preferredValue = new())
    {
        return new CancellationToken();
    }
}