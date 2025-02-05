using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

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

[GAgent("pluginTest")]
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

        State.Content.Add(data.Greeting);
    }
}