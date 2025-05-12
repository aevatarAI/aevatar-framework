namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class EventHandlerExceptionEvent : EventBase
{
    [Id(0)] public required GrainId GrainId { get; set; }
    [Id(1)] public required Type HandleEventType { get; set; }
    [Id(2)] public required string ExceptionMessage { get; set; }
}