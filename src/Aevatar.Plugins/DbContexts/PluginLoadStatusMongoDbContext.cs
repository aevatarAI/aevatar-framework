using Aevatar.Plugins.Entities;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Aevatar.Plugins.DbContexts;

[ConnectionStringName("Orleans")]
public class PluginLoadStatusMongoDbContext(IServiceProvider serviceProvider)
    : AevatarMongoDbContextBase(serviceProvider)
{
    public IMongoCollection<PluginLoadStatusDocument> PluginLoadStatus
    {
        get
        {
            InitializeModelSource();
            return Collection<PluginLoadStatusDocument>();
        }
    }

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);
        modelBuilder.Entity<PluginLoadStatusDocument>(b =>
        {
            var prefix = GetCollectionPrefix();
            b.CollectionName = $"{prefix}PluginLoadStatus";
        });
    }
}