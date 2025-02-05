using Aevatar.Core.Abstractions;
using Aevatar.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(silo =>
    {
        silo.AddMemoryGrainStorage("Default")
            .AddMemoryStreams(AevatarCoreConstants.StreamProvider)
            .AddMemoryGrainStorage("PubSubStore")
            .AddLogStorageBasedLogConsistencyProvider("LogStorage")
            .UseLocalhostClustering()
            .UseAevatar()
            .ConfigureLogging(logging => logging.AddConsole());
    })
    .UseConsoleLifetime();

using var host = builder.Build();

await host.RunAsync();