using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class EventHandlerWithResponseTestGAgentState : StateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class EventHandlerWithResponseTestStateLogEvent : StateLogEventBase<EventHandlerWithResponseTestStateLogEvent>;

[GAgent("eventHandlerWithResponseTest", "test")]
public class
    EventHandlerWithResponseTestGAgent : GAgentBase<EventHandlerWithResponseTestGAgentState,
    EventHandlerWithResponseTestStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used for testing event handler with response.");
    }

    [EventHandler]
    public async Task<NaiveTestEvent> ExecuteAsync(ResponseTestEvent responseTestEvent)
    {
        return new NaiveTestEvent
        {
            Greeting = responseTestEvent.Greeting
        };
    }
}