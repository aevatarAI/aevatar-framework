using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.ArtifactGAgents;
using Aevatar.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client =>
    {
        client.UseLocalhostClustering()
            .AddMemoryStreams(AevatarCoreConstants.StreamProvider);
        client.UseAevatar();
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
    Console.WriteLine("Get GAgent from IStateGAgent interface:");
    var gAgent = await gAgentFactory.GetGAgentAsync<IStateGAgent<MyArtifactGAgentState>>();
    var description = await gAgent.GetDescriptionAsync();
    Console.WriteLine(description);
    
    var events = await gAgent.GetAllSubscribedEventsAsync();
    foreach (var @event in events!.ToList())
    {
        Console.WriteLine($"Subscribing event: {@event}");
    }
}

{
    Console.WriteLine("Get GAgent from Type:");
    var myArtifactGAgent = await gAgentFactory.GetGAgentAsync(Guid.NewGuid(), typeof(MyArtifactGAgent));
    var description = await myArtifactGAgent.GetDescriptionAsync();
    Console.WriteLine(description);
}

Console.ReadKey();