using Aevatar.Core.Abstractions;
using Orleans.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aevatar.Core.Placement;

/// Set your Orleans Silo name pattern in the environment variable SILO_NAME_PATTERN.

var siloNamePattern = "No-Analytics-Silo";
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(silo =>
    {
        silo.AddMemoryGrainStorage("Default")
            .AddMemoryStreams(AevatarCoreConstants.StreamProvider)
            .AddMemoryGrainStorage("PubSubStore")
            .AddLogStorageBasedLogConsistencyProvider("LogStorage")
            .UseLocalhostClustering()
            .Configure<SiloOptions>(options =>
                    {
                        options.SiloName = $"{siloNamePattern}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
                    })
            .ConfigureLogging(logging => logging.AddConsole());
    })
    .UseConsoleLifetime();

builder.ConfigureServices((context, services) =>
        {
            // Register the SiloNamePatternPlacement director
            services.AddPlacementDirector<SiloNamePatternPlacement, SiloNamePatternPlacementDirector>();

        });

using var host = builder.Build();

await host.RunAsync();