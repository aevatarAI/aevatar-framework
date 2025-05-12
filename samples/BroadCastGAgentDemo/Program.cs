using Aevatar.Core.Abstractions;
using MessagingGAgent.Grains;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client =>
    {
        client.UseLocalhostClustering()
            .AddMemoryStreams(AevatarCoreConstants.StreamProvider);
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .UseConsoleLifetime();

using IHost host = builder.Build();
await host.StartAsync();

var client = host.Services.GetRequiredService<IClusterClient>();

// Create a new grain instance for the sub-agent
var subAgents = new List<IDemoGAgent>();
for (var i = 0; i < 1000; ++i)
{
    var subAgentId = Guid.NewGuid();
    var subAgent = client.GetGrain<IDemoGAgent>(subAgentId);
    await subAgent.ActivateAsync();
    subAgents.Add(subAgent);
}

// Create a new grain instance for the publisher agent
var pubAgentId = Guid.NewGuid();
var pubAgent = client.GetGrain<IDemoGAgent>(pubAgentId);

Console.WriteLine("subAgent-999 Count {0}, subAgent-1000 Count {1}", await subAgents[998].GetCount(), await subAgents[999].GetCount());

var demoEvent = new DemoEvent
{
    Number = 100,
    CorrelationId = Guid.NewGuid(),
    PublisherGrainId = pubAgent.GetGrainId(),
};

await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", demoEvent);

// Wait for the event to be processed
await Task.Delay(1000);

var count = 0;
for (var i = 0; i < 1000; ++i)
{
    if (await subAgents[i].GetCount() != 100)
    {
       count++;
    }
}

if (count > 0)
{
    Console.WriteLine("Total missing is {0}", count);
}


await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", demoEvent);

// Wait for the event to be processed
await Task.Delay(1000);

count = 0;
for (var i = 0; i < 1000; ++i)
{
    if (await subAgents[i].GetCount() != 200)
    {
       count++;
    }
}

if (count > 0)
{
    Console.WriteLine("Total missing is {0}", count);
}

Console.WriteLine("subAgent-999 Count {0}, subAgent-1000 Count {1}", await subAgents[998].GetCount(), await subAgents[999].GetCount());

// Test Unsubscribe
var subAgentId1 = Guid.NewGuid();
var subAgent1 = client.GetGrain<IDemoGAgent>(subAgentId1);
await subAgent1.ActivateAsync();
await Task.Delay(000);
await subAgent1.UnSub<DemoEvent>();
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", demoEvent);
await Task.Delay(1000);
Console.WriteLine("subAgent-unsub Count {0}, subAgent-1000 Count {1}", await subAgent1.GetCount(), await subAgents[999].GetCount());

// Test Unsubscribe without handle
var subAgentId2 = Guid.NewGuid();
var subAgent2 = client.GetGrain<IDemoGAgent>(subAgentId2);
await subAgent2.ActivateAsync();
await Task.Delay(1000);
await subAgent2.UnSubWithOutHandle<DemoEvent>();
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", demoEvent);
await Task.Delay(1000);
Console.WriteLine("subAgent-unsub-no-handle Count {0}, subAgent-1000 Count {1}", await subAgent2.GetCount(), await subAgents[999].GetCount());



Console.WriteLine("Press any key to exit..."); 

await host.StopAsync(); 