using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

[GenerateSerializer]
public class PublishingAgentState : StateBase
{
}

[GenerateSerializer]
public class PublishingStateLogEvent : StateLogEventBase<PublishingStateLogEvent>
{
}

[GAgent]
public class PublishingGAgent : GAgentBase<PublishingAgentState, PublishingStateLogEvent>, IPublishingGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("Agent to be used for publishing new events.");
    }

    public async Task PublishEventAsync<T>(T @event, params IGAgent[] agents) where T : EventBase
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        await RegisterManyAsync(agents.ToList());

        Logger.LogInformation($"PublishingAgent publish {@event}");
        await PublishAsync(@event);
    }
}