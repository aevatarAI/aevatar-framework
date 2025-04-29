using System.Diagnostics;
using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace PluginGAgent.Grains;

[GenerateSerializer]

public class WorkerGAgentState : StateBase;

[GenerateSerializer]
public class WorkerStateLogEvent : StateLogEventBase<WorkerStateLogEvent>;

[GAgent("worker", "pluginTest")]
public class WorkerGAgent : GAgentBase<WorkerGAgentState, WorkerStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for");
    }

    [EventHandler]
    public async Task HandleEventAsync(Command data)
    {
        Logger.LogInformation("Received command: {0}", data.Content);

        var client = new RestClient("https://www.hao123.com");
        var request = new RestRequest("/api/gethitthecity");
        var response = await client.GetAsync(request);
        Logger.LogInformation($"Response: {response.Content}");
    }
}