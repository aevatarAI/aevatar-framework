using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class GroupGAgentState : StateBase
{
    [Id(0)]  public int RegisteredGAgents { get; set; } = 0;
}

public class GroupStateLogEvent : StateLogEventBase<GroupStateLogEvent>
{
    
}

[GAgent("group", "test")]
public class GroupGAgent : GAgentBase<GroupGAgentState, GroupStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("An agent to inform other agents when a social event is published.");
    }

    protected override Task OnRegisterAgentAsync(GrainId agentGuid)
    {
        RaiseEvent(new IncrementStateLogEvent());
        return Task.CompletedTask;
    }

    protected override Task OnUnregisterAgentAsync(GrainId agentGuid)
    {
        RaiseEvent(new DecrementStateLogEvent());
        return Task.CompletedTask;
    }

    protected override void GAgentTransitionState(GroupGAgentState state, StateLogEventBase<GroupStateLogEvent> @event)
    {
        if (@event is IncrementStateLogEvent)
        {
            State.RegisteredGAgents++;
        }
        else if (@event is DecrementStateLogEvent)
        {
            State.RegisteredGAgents--;
        }
    }

    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        State.RegisteredGAgents = 0;
    }

    [GenerateSerializer]
    public class IncrementStateLogEvent : StateLogEventBase<GroupStateLogEvent>;

    [GenerateSerializer]
    public class DecrementStateLogEvent : StateLogEventBase<GroupStateLogEvent>;
}