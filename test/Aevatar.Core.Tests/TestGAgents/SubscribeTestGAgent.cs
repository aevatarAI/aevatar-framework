using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class SubscribeTestGAgentState : StateBase
{
    [Id(0)]  public Dictionary<Type, List<Type>> SubscriptionInfo { get; set; }
}

public class SubscribeTestStateLogEvent : StateLogEventBase<SubscribeTestStateLogEvent>;

[GAgent("subscribeTest", "test")]
public class SubscribeTestGAgent : GAgentBase<SubscribeTestGAgentState, SubscribeTestStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used to test implementation of GetAllSubscribedEventsAsync.");
    }
    
    public async Task HandleEventAsync(SubscribedEventListEvent eventData)
    {
        if (State.SubscriptionInfo.IsNullOrEmpty())
        {
            State.SubscriptionInfo = eventData.Value;
        }
    }
}