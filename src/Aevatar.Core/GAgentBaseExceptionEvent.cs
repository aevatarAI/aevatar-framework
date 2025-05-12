using Aevatar.Core.Abstractions;

namespace Aevatar.Core;

[GenerateSerializer]
public class GAgentBaseExceptionEvent : EventBase
{
    [Id(0)] public required GrainId GrainId { get; set; }
    [Id(1)] public required string ExceptionMessage { get; set; }
}