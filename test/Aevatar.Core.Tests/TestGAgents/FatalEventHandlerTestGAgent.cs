using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class FatalEventHandlerTestGAgentState
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class FatalEventHandlerTestStateLogEvent : StateLogEventBase;

[GAgent]
public class FatalEventHandlerTestGAgent : GAgentBase<EventHandlerTestGAgentState, EventHandlerTestStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used for testing invalid event handlers.");
    }

    // This will be recognized as an event handler,
    // but will throw an exception because NotImplEventBaseTestEvent is not derived from EventBase.
    public Task<NotImplEventBaseTestEvent> HandleEventAsync(ResponseTestEvent eventData)
    {
        return Task.FromResult(new NotImplEventBaseTestEvent());
    }
    
    // This will be recognized as an event handler,
    // but will throw an exception because this method doesn't have response event.
    public Task HandleEventAsync(AnotherResponseTestEvent eventData)
    {
        return Task.CompletedTask;
    }
}