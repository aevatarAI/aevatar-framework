using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestStateLogEvents;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class PublishingAgentState : StateBase
{
}

[GAgent("publishing")]
public class PublishingGAgent : GAgentBase<PublishingAgentState, PublishingStateLogEvent>, IPublishingGAgent
{
    public PublishingGAgent(ILogger<PublishingGAgent> logger) : base(logger)
    {
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("Agent to be used for publishing new events.");
    }

    public async Task PublishEventAsync<T>(T @event) where T : EventBase
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        Logger.LogInformation($"PublishingAgent publish {@event}");
        await PublishAsync(@event);
    }
}