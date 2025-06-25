using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class TestStateProjectionGAgentState : StateBase
{
    [Id(0)] public bool StateHandlerCalled { get; set; }
}

[GenerateSerializer]
public class TestStateProjectionStateLogEvent : StateLogEventBase<TestStateProjectionStateLogEvent>;

public interface ITestStateProjectionGAgent : IStateGAgent<TestStateProjectionGAgentState>;

[GAgent]
public class TestStateProjectionGAgent : StateProjectionGAgentBase<GroupGAgentState,
    TestStateProjectionGAgentState, TestStateProjectionStateLogEvent>, ITestStateProjectionGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing state projection gagent base.");
    }

    protected override async Task HandleStateAsync(StateWrapper<GroupGAgentState> projectionState)
    {
        RaiseEvent(new CallStateHandlerStateLogEvent());
        await ConfirmEvents();
    }

    protected override void GAgentTransitionState(TestStateProjectionGAgentState state,
        StateLogEventBase<TestStateProjectionStateLogEvent> @event)
    {
        if (@event is CallStateHandlerStateLogEvent)
        {
            State.StateHandlerCalled = true;
        }
    }

    [GenerateSerializer]
    public class CallStateHandlerStateLogEvent : StateLogEventBase<TestStateProjectionStateLogEvent>;
}