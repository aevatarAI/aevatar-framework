using System.Diagnostics;
using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace Aevatar.GAgents.Plugins;

[GenerateSerializer]
public class PluginTestGAgentState : StateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class PluginTestStateLogEvent : StateLogEventBase<PluginTestStateLogEvent>
{
    [Id(0)] public override Guid Id { get; set; }
}

[GenerateSerializer]
public class PluginTestEvent : EventBase
{
    [Id(0)] public string Greeting { get; set; }
}

[GAgent("pluginTest5")]
public class PluginTestGAgent : GAgentBase<PluginTestGAgentState, PluginTestStateLogEvent, EventBase>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a naive test GAgent");
    }

    public async Task HandleEventAsync(PluginTestEvent data)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = [];
        }

        Logger.LogInformation("Received data: {0}", data.Greeting);

        var assembly = Assembly.GetAssembly(typeof(RestClient));
        var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

        Logger.LogInformation($"产品版本: {versionInfo.ProductVersion}");
        Logger.LogInformation($"文件版本: {versionInfo.FileVersion}");
        Logger.LogInformation($"程序集版本: {assembly.GetName().Version}");

        var client = new RestClient("https://www.hao123.com");
        var request = new RestRequest("/api/gethitthecity");
        var response = await client.GetAsync(request);
        Logger.LogInformation($"Response: {response.Content}");

        State.Content.Add(data.Greeting);
    }
}