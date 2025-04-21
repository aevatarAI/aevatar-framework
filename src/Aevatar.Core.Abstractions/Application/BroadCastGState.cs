namespace Aevatar.Core.Abstractions;

using Orleans.Streams;

[GenerateSerializer]
public class BroadCastGState : StateBase
{
    public Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>> Subscription = [];
}