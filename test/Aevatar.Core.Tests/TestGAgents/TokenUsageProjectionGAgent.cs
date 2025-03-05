using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests;

[GenerateSerializer]
public class TokenUsageProjectionGAgentState : StateBase
{
    [Id(0)] public long TotalUsedToken { get; set; }
}

[GenerateSerializer]
public class TokenUsageProjectionStateLogEvent : StateLogEventBase<TokenUsageProjectionStateLogEvent>
{

}

[GAgent]
public class TokenUsageProjectionGAgent : StateProjectionGAgentBase<SampleAIGAgentState, TokenUsageProjectionGAgentState, TokenUsageProjectionStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing token usage projection.");
    }
    
    protected override async Task HandleStateAsync(StateWrapper<SampleAIGAgentState> projectionStateWrapper)
    {
        RaiseEvent(new TokenUsageStateLogEvent
        {
            TotalUsageToken = projectionStateWrapper.State.LatestTotalUsageToken
        });
        await ConfirmEvents();
    }

    protected override void GAgentTransitionState(TokenUsageProjectionGAgentState state, StateLogEventBase<TokenUsageProjectionStateLogEvent> @event)
    {
        if (@event is TokenUsageStateLogEvent tokenUsageStateLogEvent)
        {
            State.TotalUsedToken += tokenUsageStateLogEvent.TotalUsageToken;
        }
    }

    [GenerateSerializer]
    public class TokenUsageStateLogEvent : StateLogEventBase<TokenUsageProjectionStateLogEvent>
    {
        [Id(0)] public long InputToken { get; set; }
        [Id(1)] public long OutputToken { get; set; }
        [Id(2)] public long TotalUsageToken { get; set; }
    }
}