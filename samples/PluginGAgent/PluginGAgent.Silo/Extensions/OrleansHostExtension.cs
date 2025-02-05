using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.EventSourcing.MongoDB.Hosting;
using Aevatar.Extensions;
using Aevatar.Plugins;
using Aevatar.Plugins.Extensions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Orleans.Configuration;
using Orleans.Serialization;
using Orleans.Serialization.Serializers;

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
                        options.DatabaseName = "AISmartDb";
                    })
                    .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug).AddConsole(); })
                    .AddMongoDbStorageBasedLogConsistencyProvider("LogStorage", options =>
                    {
                        options.ClientSettings =
                            MongoClientSettings.FromConnectionString("mongodb://localhost:27017/?maxPoolSize=555");
                        options.Database = "AISmartDb";
                    })
                    .AddMemoryStreams("Aevatar")
                    .UseAevatar<PluginGAgentTestModule>();
            })
            .UseConsoleLifetime();
    }
}