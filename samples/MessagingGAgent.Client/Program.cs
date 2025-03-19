using Aevatar.Core.Abstractions;
using MessagingGAgent.Grains.Agents.Events;
using MessagingGAgent.Grains.Agents.Group;
using MessagingGAgent.Grains.Agents.Messaging;
using MessagingGAgent.Grains.Agents.Publisher;
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

var parentId = Guid.NewGuid();
//var messagingParentAgent = client.GetGrain<IMessagingGAgent>(parentId);
var parentAgent = client.GetGrain<IGroupGAgent>(parentId);

List<IMessagingGAgent> messagingAgents = [];
var maxAgents = 10;
for(var i = 0; i < maxAgents; ++i)
{
    var messagingAgentId = Guid.NewGuid();
    var messagingAgent = client.GetGrain<IMessagingGAgent>(messagingAgentId);
    await parentAgent.RegisterAsync(messagingAgent);
    
    messagingAgents.Add(messagingAgent);
}

var publisher = client.GetGrain<IPublishingGAgent>(Guid.NewGuid());
await parentAgent.RegisterAsync(publisher);
await publisher.PublishEventAsync(new SendEvent()
{
    Message = "Hello, World!"
});
Console.WriteLine("Published event. Waiting for agents to receive messages...");
await Task.Delay(maxAgents * 1000);

var completed = 0;
foreach (var agent in messagingAgents)
{
    var receivedMessages = await agent.GetReceivedMessagesAsync();
    if (receivedMessages != maxAgents)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Agent did not receive the expected number of messages. " + receivedMessages);

        continue;
    }
    completed++;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Completed: " + completed);

await host.StopAsync();