using Aevatar.Core;
using Aevatar.Core.Abstractions;

namespace PluginGAgent.Grains;

[GenerateSerializer]
public class CommanderGAgentState : StateBase
{

}

[GenerateSerializer]
public class CommanderStateLogEvent : StateLogEventBase<CommanderStateLogEvent>
{

}

[GenerateSerializer]
public class Command : EventBase
{
    [Id(0)] public string Content { get; set; }
}

[GAgent("commander", "pluginTest")]
public class CommanderGAgent : GAgentBase<CommanderGAgentState, CommanderStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for");
    }
}