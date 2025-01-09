using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestGEvents;

[GenerateSerializer]
public class MessageGEvent : GEventBase
{
    public override Guid Id { get; set; }= Guid.NewGuid();
}