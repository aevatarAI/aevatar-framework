using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestStateLogEvents;

[GenerateSerializer]
public class MessageStateLogEvent : StateLogEventBase
{
    public override Guid Id { get; set; }= Guid.NewGuid();
}