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
var subAgentId = Guid.NewGuid();
var subAgent = client.GetGrain<IDemoGAgent>(subAgentId);

// Create a new grain instance for the publisher agent
var pubAgentId = Guid.NewGuid();
var pubAgent = client.GetGrain<IDemoGAgent>(pubAgentId);

Console.WriteLine("Count {0}", await subAgent.GetCount());

var demoEvent = new DemoEvent
{
    Number = 100,
    CorrelationId = Guid.NewGuid(),
    PublisherGrainId = subAgent.GetGrainId(),
};

var streamProvider = client.GetStreamProvider(AevatarCoreConstants.StreamProvider);
var streamId = StreamId.Create("Aevatar", subAgent.GetGrainId().ToString());
var stream = streamProvider.GetStream<EventWrapperBase>(streamId);
Console.WriteLine("stream Id is {0}", streamId);
    
await stream.OnNextAsync(new EventWrapper<DemoEvent>(new DemoEvent
    {
        Number = demoEvent.Number,
        CorrelationId = demoEvent.CorrelationId,
        PublisherGrainId = subAgent.GetGrainId(),
    },
    Guid.NewGuid(),
    pubAgent.GetGrainId()));

// Wait for the event to be processed
await Task.Delay(100);

Console.WriteLine("Count {0}", await subAgent.GetCount());

Console.WriteLine("Press any key to exit..."); 

await host.StopAsync(); 