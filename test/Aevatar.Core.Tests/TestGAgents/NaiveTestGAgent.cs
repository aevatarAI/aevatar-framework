using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class NaiveTestGAgentState : StateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class NaiveTestStateLogEvent : StateLogEventBase<NaiveTestStateLogEvent>
{
    [Id(0)] public Guid Id { get; set; }
}

[GenerateSerializer]
public class NaiveGAgentConfiguration : ConfigurationBase
{
    [Id(0)] public string Greeting { get; set; } = string.Empty;
}

[GAgent("naiveTest")]
public class
    NaiveTestGAgent : GAgentBase<NaiveTestGAgentState, NaiveTestStateLogEvent, EventBase, NaiveGAgentConfiguration>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a naive test GAgent");
    }

    protected override async Task PerformConfigAsync(NaiveGAgentConfiguration configuration)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = [];
        }

        State.Content.Add(configuration.Greeting);
    }
}