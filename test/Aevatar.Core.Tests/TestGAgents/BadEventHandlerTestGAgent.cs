using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class BadEventHandlerTestGAgentState : EventHandlerTestGAgentState
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class BadEventHandlerTestStateLogEvent : StateLogEventBase;

[GAgent("badEventHandlerTest")]
public class BadEventHandlerTestGAgent : GAgentBase<BadEventHandlerTestGAgentState, EventHandlerTestStateLogEvent>
{
    public BadEventHandlerTestGAgent(ILogger<BadEventHandlerTestGAgent> logger) : base(logger)
    {
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used for testing invalid event handlers.");
    }

    // This won't be recognized as an event handler,
    // because the method name doesn't match `HandleEventAsync`,
    // and doesn't have `EventHandler` attribute.
    public Task ExecuteAsync(NaiveTestEvent eventData)
    {
        return Task.CompletedTask;
    }

    // This won't be recognized as an event handler,
    // because the parameter is not EventWrapperBase.
    [AllEventHandler]
    public Task HandleAsync(NaiveTestEvent eventData)
    {
        return Task.CompletedTask;
    }

    // This won't be recognized as an event handler,
    // because IncorrectTestEvent is not inherit from EventBase.
    [EventHandler]
    public Task HandleEventAsync(IncorrectTestEvent eventData)
    {
        return Task.CompletedTask;
    }
}