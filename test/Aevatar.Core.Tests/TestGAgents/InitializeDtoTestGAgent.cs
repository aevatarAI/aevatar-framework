using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class InitializeDtoTestGAgentState : StateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class InitializeDtoTestGEvent : StateLogEventBase<InitializeDtoTestGEvent>
{
    [Id(0)] public Guid Id { get; set; }
}

public class InitializeDto : InitializeDtoBase
{
    [Id(0)] public string InitialGreeting { get; set; }
}

[GAgent("initialize")]
public class InitializeDtoTestGAgent : GAgentBase<InitializeDtoTestGAgentState,
    InitializeDtoTestGEvent,EventBase, InitializeDto>
{
    public InitializeDtoTestGAgent(ILogger<InitializeDtoTestGAgent> logger) : base(logger)
    {
    }

    public override Task InitializeAsync(InitializeDto initializeDto)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = [];
        }

        State.Content.Add(initializeDto.InitialGreeting);
        return Task.CompletedTask;
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a test GAgent for initialization testing.");
    }
}