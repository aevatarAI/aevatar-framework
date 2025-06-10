using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MongoDB;

namespace Aevatar.Plugins.DbContexts;

public class AevatarMongoDbContextBase : AbpMongoDbContext
{
    private readonly IServiceProvider _serviceProvider;

    public AevatarMongoDbContextBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        LazyServiceProvider = new AbpLazyServiceProvider(serviceProvider);
    }

    protected void InitializeModelSource()
    {
        ModelSource = new MongoModelSource();
        ModelSource.GetModel(this);
    }

    public override void InitializeDatabase(IMongoDatabase database, IMongoClient client,
        IClientSessionHandle? sessionHandle)
    {
        var options = _serviceProvider.GetRequiredService<IOptions<PluginGAgentLoadOptions>>().Value;
        base.InitializeDatabase(database, client, sessionHandle);
    }

    protected string GetCollectionPrefix()
    {
        var pluginsOptions = _serviceProvider.GetRequiredService<IOptions<PluginGAgentLoadOptions>>().Value;
        var hostId = pluginsOptions.HostId;
        return hostId.IsNullOrEmpty() ? "StreamStorage" : $"Stream{hostId}";
    }
}