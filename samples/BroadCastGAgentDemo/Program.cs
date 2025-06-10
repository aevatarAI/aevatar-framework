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
await Task.Delay(1000);
// Test Unsubscribe
var subAgentId1 = Guid.NewGuid();
var subAgent1 = client.GetGrain<IDemoGAgent>(subAgentId1);
await subAgent1.ActivateAsync();
await Task.Delay(1000);
await subAgent1.UnSub<DemoEvent>();
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", demoEvent);
await Task.Delay(1000);
Console.WriteLine("subAgent-unsub Count {0}, subAgent-1000 Count {1}", await subAgent1.GetCount(), await subAgents[999].GetCount());

// Test Subscribe Multiple Events
Console.WriteLine("\n=== Testing Multiple Event Types Subscription ===");

// Create a new agent for testing multiple events
var multiEventAgentId = Guid.NewGuid();
var multiEventAgent = client.GetGrain<IDemoBatchSubGAgent>(multiEventAgentId);
await multiEventAgent.ActivateAsync();
await Task.Delay(1000);  // Give time for subscriptions to be set up

// Create events of different types
var addEvent = new DemoEvent
{
    Number = 10,
    CorrelationId = Guid.NewGuid(),
    PublisherGrainId = pubAgent.GetGrainId(),
};

var multiplyEvent = new DemoMutiplyEvent
{
    Number = 5,
    CorrelationId = Guid.NewGuid(),
    PublisherGrainId = pubAgent.GetGrainId(),
};

var divideEvent = new DemoDivideEvent
{
    Number = 2,
    CorrelationId = Guid.NewGuid(),
    PublisherGrainId = pubAgent.GetGrainId(),
};

// First broadcast add event to set initial value
Console.WriteLine("Broadcasting add event with value 10...");
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", addEvent);
await Task.Delay(1000);
Console.WriteLine($"After add event: Count = {await multiEventAgent.GetCount()}");  // Should be 10

// Then broadcast multiply event 
Console.WriteLine("Broadcasting multiply event with value 5...");
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", multiplyEvent);
await Task.Delay(1000);
// Now we're actually multiplying, not just adding
Console.WriteLine($"After multiply event: Count = {await multiEventAgent.GetCount()}");  // Should be 50 (10 * 5)

// Then broadcast divide event
Console.WriteLine("Broadcasting divide event with value 2...");
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", divideEvent);
await Task.Delay(1000);
// Now we're actually dividing, not just adding
Console.WriteLine($"After divide event: Count = {await multiEventAgent.GetCount()}");  // Should be 25 (50 / 2)

// Test all events in quick succession
Console.WriteLine("\nTesting rapid-fire broadcast of all event types...");
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", addEvent);
await Task.Delay(1000);
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", multiplyEvent);
await Task.Delay(1000);
await pubAgent.BroadCastEventAsync("DemoScheduleGAgent", divideEvent);
await Task.Delay(1000);
// Add 10, multiply by 5, divide by 2: (25 + 10) * 5 / 2 = 175 / 2 = 87.5 (integer division = 87)
Console.WriteLine($"After all events: Count = {await multiEventAgent.GetCount()}");

Console.WriteLine("Multiple event subscription test completed.");
Console.WriteLine();

Console.WriteLine("Press any key to exit..."); 

await host.StopAsync(); 