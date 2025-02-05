using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class ConfigurationTestGAgentState : StateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

[GenerateSerializer]
public class ConfigurationTestStateLogEvent : StateLogEventBase<ConfigurationTestStateLogEvent>
{
    [Id(0)] public Guid Id { get; set; }
}

[GenerateSerializer]
public class Configuration : ConfigurationBase
{
    [Id(0)] public string InitialGreeting { get; set; }
}

[GAgent("configurationTest")]
public class ConfigurationTestGAgent : GAgentBase<ConfigurationTestGAgentState,
    ConfigurationTestStateLogEvent, EventBase, Configuration>
{
    protected override Task PerformConfigAsync(Configuration configuration)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = [];
        }

        State.Content.Add(configuration.InitialGreeting);
        return Task.CompletedTask;
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a test GAgent for initialization testing.");
    }
}