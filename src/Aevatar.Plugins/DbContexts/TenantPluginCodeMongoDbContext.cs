using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Aevatar.Plugins.DbContexts;

[ConnectionStringName("Orleans")]
public class TenantPluginCodeMongoDbContext(IServiceProvider serviceProvider)
    : AevatarMongoDbContextBase(serviceProvider)
{
    public IMongoCollection<TenantPluginCodeSnapshotDocument> TenantPluginCode
    {
        get
        {
            InitializeModelSource();
            return Collection<TenantPluginCodeSnapshotDocument>();
        }
    }

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);
        modelBuilder.Entity<TenantPluginCodeSnapshotDocument>(b =>
        {
            var prefix = GetCollectionPrefix();
            b.CollectionName = $"{prefix}{typeof(TenantPluginCodeGAgent).FullName!}";
        });
    }
}