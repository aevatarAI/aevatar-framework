using Aevatar.EventSourcing.MongoDB.Hosting;
using Aevatar.Extensions;
using Aevatar.PermissionManagement.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace PluginGAgent.Silo.Extensions;

public static class OrleansHostExtension
{
    public static IHostBuilder UseOrleansConfiguration(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseOrleans((context, siloBuilder) =>
            {
                siloBuilder
                    .UseLocalhostClustering()
                    .UseMongoDBClient("mongodb://localhost:27017/?maxPoolSize=555")
                    .AddMongoDBGrainStorage("PubSubStore", options =>
                    {
                        options.CollectionPrefix = "StreamStorage";
                        options.DatabaseName = "AevatarDb";
                    })
                    .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug).AddConsole(); })
                    .AddMongoDbStorageBasedLogConsistencyProvider("LogStorage", options =>
                    {
                        options.ClientSettings =
                            MongoClientSettings.FromConnectionString("mongodb://localhost:27017/?maxPoolSize=555");
                        options.Database = "AevatarDb";
                    })
                    .AddMemoryStreams("Aevatar")
                    .UseAevatarPermissionManagement()
                    .UseAevatar()
                    ;
            })
            .UseConsoleLifetime();
    }
}