using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestStateLogEvents;

[GenerateSerializer]
public class MessageStateLogEvent : StateLogEventBase
{
    [Id(0)] public Guid Id { get; set; }= Guid.NewGuid();
}