using Aevatar.ArtifactGAgents;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.Extensions;
using Aevatar.PermissionManagement;
using Aevatar.Plugins;
using Aevatar.Plugins.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PluginGAgent.Grains;

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(client =>
    {
        client.UseLocalhostClustering()
            .UseMongoDBClient("mongodb://localhost:27017/?maxPoolSize=555")
            .AddMemoryStreams(AevatarCoreConstants.StreamProvider)
            .UseAevatar();
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .UseConsoleLifetime();

using var host = builder.Build();
await host.StartAsync();

var pluginManager = host.Services.GetRequiredService<IPluginGAgentManager>();
var gAgentFactory = host.Services.GetRequiredService<IGAgentFactory>();
var gAgentManager = host.Services.GetRequiredService<IGAgentManager>();

Console.WriteLine("Select an option:");
Console.WriteLine("0. Add plugin code");
Console.WriteLine("1. Try Execute Plugin GAgents");
Console.WriteLine("2. Try Get Artifact GAgent");
Console.WriteLine("3. Show all candidate GAgents");
Console.WriteLine("4. Call PermissionGAgent");
var choice = Console.ReadLine();

switch (choice)
{
    case "0":
        await AddCodeAsync(pluginManager);
        break;
    case "1":
        await PerformCommandAsync(gAgentFactory);
        break;
    case "2":
        await TryArtifactGAgentAsync(gAgentFactory);
        break;
    case "3":
        ListCandidateGAgents(gAgentManager);
        break;
    case "4":
        await CallPermissionGAgent(gAgentFactory);
        break;
    default:
        Console.WriteLine("Invalid choice.");
        break;
}

async Task AddCodeAsync(IPluginGAgentManager pluginGAgentManager)
{
    var plugins = PluginLoader.LoadPlugins("plugins");

    var tenantId = "test".ToGuid();
    foreach (var code in plugins)
    {
        await pluginGAgentManager.AddPluginAsync(new AddPluginDto
        {
            Code = code,
            TenantId = tenantId
        });
    }
}

async Task PerformCommandAsync(IGAgentFactory factory)
{
    var publishingGAgent = await factory.GetGAgentAsync<IPublishingGAgent>();
    var commander = await factory.GetGAgentAsync(GrainId.Create("pluginTest.commander", Guid.NewGuid().ToString("N")));
    var worker = await factory.GetGAgentAsync("worker", "pluginTest");
    await publishingGAgent.RegisterAsync(commander);
    await commander.RegisterAsync(worker);
    await publishingGAgent.PublishEventAsync(new Command { Content = "test" });
}

async Task TryArtifactGAgentAsync(IGAgentFactory factory)
{
    var myArtifactGAgent =
        await factory.GetGAgentAsync(typeof(MyArtifactGAgent));
    var description = await myArtifactGAgent.GetDescriptionAsync();
    Console.WriteLine(description);
}

void ListCandidateGAgents(IGAgentManager manager)
{
    Console.WriteLine("Available GAgent GrainTypes:");
    var grainTypes = manager.GetAvailableGAgentGrainTypes();
    foreach (var grainType in grainTypes)
    {
        Console.WriteLine(grainType.ToString());
    }

    Console.WriteLine("Available Types:");
    var types = manager.GetAvailableGAgentTypes();
    foreach (var type in types)
    {
        Console.WriteLine(type.FullName);
    }
}

async Task CallPermissionGAgent(IGAgentFactory factory)
{
    RequestContext.Set("CurrentUser", new UserContext
    {
        UserId = "TestUser".ToGuid(),
        Roles = ["User", "Admin"]
    });
    var permissionGAgent = await factory.GetGAgentAsync<IPermissionGAgent>();
    await permissionGAgent.DoSomething1Async();
}