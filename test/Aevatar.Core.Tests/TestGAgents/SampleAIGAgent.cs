using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests;

[GenerateSerializer]
public class SampleAIGAgentState : StateBase
{
    [Id(0)] public int LatestTotalUsageToken { get; set; }
}

[GenerateSerializer]
public class SampleAIStateLogEvent : StateLogEventBase<SampleAIStateLogEvent>;

public interface ISampleAIGAgent : IStateGAgent<SampleAIGAgentState>
{
    Task PretendingChatAsync(string message);
}

[GAgent]
public class SampleAIGAgent : GAgentBase<SampleAIGAgentState, SampleAIStateLogEvent>, ISampleAIGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("An AI GAgent sample to test state projection.");
    }

    public async Task PretendingChatAsync(string message)
    {
        Logger.LogInformation("Call PretendingChatAsync");
        var tokenUsage = new TokenUsageStateLogEvent
        {
            GrainId = this.GetPrimaryKey(),
            InputToken = message.Length,
            OutputToken = 2000,
            TotalUsageToken = 2000 + message.Length
        };
        RaiseEvent(tokenUsage);
        await ConfirmEvents();
    }

    protected override void GAgentTransitionState(SampleAIGAgentState state,
        StateLogEventBase<SampleAIStateLogEvent> @event)
    {
        if (@event is TokenUsageStateLogEvent tokenUsageStateLogEvent)
        {
            State.LatestTotalUsageToken = tokenUsageStateLogEvent.TotalUsageToken;
        }
        else
        {
            State.LatestTotalUsageToken = 0;
        }
    }

    [GenerateSerializer]
    public class TokenUsageStateLogEvent : StateLogEventBase<SampleAIStateLogEvent>
    {
        [Id(0)] public int InputToken { get; set; }
        [Id(1)] public int OutputToken { get; set; }
        [Id(2)] public int TotalUsageToken { get; set; }
        [Id(3)] public Guid GrainId { get; set; }
    }
}