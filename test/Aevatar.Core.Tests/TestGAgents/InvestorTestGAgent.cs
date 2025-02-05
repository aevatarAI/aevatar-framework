using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

public interface IInvestorTestGAgent: IGAgent
{
    
}

[GenerateSerializer]
public class InvestorTestGAgentState : NaiveTestGAgentState
{
    
}

[GAgent("investor", "test")]
public class InvestorTestGAgent : GAgentBase<InvestorTestGAgentState, NaiveTestStateLogEvent>, IInvestorTestGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent acts as a investor.");
    }

    public async Task HandleEventAsync(WorkingOnTestEvent eventData)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = [];
        }

        State.Content.Add(eventData.Description);

        await PublishAsync(new InvestorFeedbackTestEvent
        {
            Content = $"This is the feedback for the event: {eventData.Description}"
        });
    }
}