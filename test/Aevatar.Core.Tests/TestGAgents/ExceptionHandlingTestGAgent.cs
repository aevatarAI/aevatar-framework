using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class ExceptionHandlingTestGAgentState : StateBase
{
    [Id(0)] public List<string> ErrorMessages { get; set; } = [];
}

[GenerateSerializer]
public class ExceptionHandlingTestStateLogEvent : StateLogEventBase<ExceptionHandlingTestStateLogEvent>
{

}

[GAgent]
public class ExceptionHandlingTestGAgent : GAgentBase<ExceptionHandlingTestGAgentState, ExceptionHandlingTestStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing exception handling.");
    }

    [EventHandler]
    public async Task HandleEventHandlingExceptionAsync(EventHandlerExceptionEvent @event)
    {
        State.ErrorMessages.Add(@event.ExceptionMessage);
    }
}