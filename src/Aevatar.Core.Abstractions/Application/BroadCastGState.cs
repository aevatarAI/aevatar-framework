namespace Aevatar.Core.Abstractions;

using Orleans.Streams;

[GenerateSerializer]
public class BroadCastGState : StateBase
{
    [Id(0)]public Dictionary<string, Guid> Subscription = [];
}