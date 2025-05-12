using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestArtifacts;
using Aevatar.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client =>
    {
        client.UseLocalhostClustering()
            .AddMemoryStreams(AevatarCoreConstants.StreamProvider)
            .UseAevatar();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .UseConsoleLifetime();

using IHost host = builder.Build();
await host.StartAsync();

var gAgentFactory = host.Services.GetRequiredService<IGAgentFactory>();
var gAgentManager = host.Services.GetRequiredService<IGAgentManager>();

var allGAgents = gAgentManager.GetAvailableGAgentTypes();
Console.WriteLine("All types:");
foreach (var gAgent in allGAgents)
{
    Console.WriteLine(gAgent.FullName);
}

Console.WriteLine();

{
    Console.WriteLine("Get GAgent from Type:");
    var myArtifactGAgent =
        await gAgentFactory.GetArtifactGAgentAsync<MyArtifact, MyArtifactGAgentState, MyArtifactStateLogEvent>();
    var description = await myArtifactGAgent.GetDescriptionAsync();
    Console.WriteLine(description);
}

Console.ReadKey();