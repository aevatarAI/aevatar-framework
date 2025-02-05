using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace MessagingGAgent.Grains.Agents.Publisher;

[GenerateSerializer]
public class PublishingAgentState : StateBase
{
}

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class PublishingGAgent : GAgentBase<PublishingAgentState, PublishingStateLogEvent>, IPublishingGAgent
{
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