using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestGEvents;

[GenerateSerializer]
public class MessageGEvent : StateLogEventBase
{
    public override Guid Id { get; set; }= Guid.NewGuid();
}