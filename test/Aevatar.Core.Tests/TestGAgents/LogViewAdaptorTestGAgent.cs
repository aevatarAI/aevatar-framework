using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

public interface ILogViewAdaptorTestGAgent
{
    
}

[GAgent("logViewAdaptorTest", "test")]
public class LogViewAdaptorTestGAgent
    : GAgentBase<LogViewAdaptorTestGState, LogViewAdaptorTestStateLogEvent>, ILogViewAdaptorTestGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        throw new NotImplementedException();
    }

    public async Task HandleEventAsync(NaiveTestEvent eventData)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = new Dictionary<Guid, LogViewAdaptorTestStateLogEvent>();
        }
        RaiseEvent(new LogViewAdaptorTestStateLogEvent
        {
            Greeting = eventData.Greeting
        });
        await ConfirmEvents();
    }
}

[GenerateSerializer]
public class LogViewAdaptorTestGState : StateBase
{
    [Id(0)] public Guid Id { get; set; }

    [Id(1)] public Dictionary<Guid, LogViewAdaptorTestStateLogEvent> Content { get; set; } = new();

    public void Apply(LogViewAdaptorTestStateLogEvent gEvent)
    {
        if (Id == Guid.Empty)
        {
            Id = Guid.NewGuid();
        }

        Content[gEvent.Id] = gEvent;
    }
}

[GenerateSerializer]
public class LogViewAdaptorTestStateLogEvent : StateLogEventBase<LogViewAdaptorTestStateLogEvent>
{
    [Id(0)] public Guid Id { get; set; } = Guid.NewGuid();
    [Id(2)] public string Greeting { get; set; }
}