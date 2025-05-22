using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Aevatar.Plugins.DbContexts;

[ConnectionStringName("Orleans")]
public class PluginCodeStorageMongoDbContext(IServiceProvider serviceProvider)
    : AevatarMongoDbContextBase(serviceProvider)
{
    public IMongoCollection<PluginCodeStorageSnapshotDocument> PluginCodeStorage
    {
        get
        {
            InitializeModelSource();
            return Collection<PluginCodeStorageSnapshotDocument>();
        }
    }

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);
        
        modelBuilder.Entity<PluginCodeStorageSnapshotDocument>(b =>
        {
            var prefix = GetCollectionPrefix();
            b.CollectionName = $"{prefix}{typeof(PluginCodeStorageGAgent).FullName!}";
        });
    }
}