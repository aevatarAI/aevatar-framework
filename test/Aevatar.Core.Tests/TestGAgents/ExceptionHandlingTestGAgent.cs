using Aevatar.Core.Abstractions;
using System.Runtime.CompilerServices;

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

    // Implementation of test methods
    public Task<Guid> TestPublishExceptionAsync(Exception exception, object? contextData = null)
    {
        return PublishExceptionAsync(exception, contextData);
    }

    public Task<Guid> TestCatchAndPublishWithoutResultAsync(bool throwException, bool rethrowException = true)
    {
        return CatchAndPublishExceptionAsync(
            async () =>
            {
                await Task.Delay(10);
                if (throwException)
                {
                    throw new InvalidOperationException("Test exception");
                }
            },
            new { TestSource = "TestCatchAndPublishWithoutResultAsync" },
            rethrowException);
    }

    public Task<(int Result, Guid ExceptionId)> TestCatchAndPublishWithResultAsync(
        bool throwException, bool rethrowException = true)
    {
        return CatchAndPublishExceptionAsync(
            async () =>
            {
                await Task.Delay(10);
                if (throwException)
                {
                    throw new InvalidOperationException("Test exception");
                }
                return 42;
            },
            0,
            new { TestSource = "TestCatchAndPublishWithResultAsync" },
            rethrowException);
    }

    public Task<(int Result, Guid ExceptionId)> TestCatchAndPublishWithCustomDefaultAsync()
    {
        return CatchAndPublishExceptionAsync(
            async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Test exception with custom default");
            },
            999,
            new { TestSource = "TestCatchAndPublishWithCustomDefaultAsync" },
            false);
    }
}