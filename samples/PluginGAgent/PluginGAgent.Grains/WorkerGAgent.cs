using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace PluginGAgent.Grains;

[GenerateSerializer]

public class WorkerGAgentState : StateBase
{

}

[GenerateSerializer]
public class WorkerStateLogEvent : StateLogEventBase<WorkerStateLogEvent>
{

}

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
    }
}