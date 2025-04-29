namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class ExceptionEvent : EventBase
{
    [Id(0)] public required GrainId GrainId { get; set; }
    [Id(1)] public required string ExceptionMessage { get; set; }
    [Id(2)] public required string ExceptionType { get; set; }
    [Id(3)] public required string StackTrace { get; set; }
    [Id(4)] public required string ContextData { get; set; }
    [Id(5)] public required DateTime Timestamp { get; set; } = DateTime.UtcNow;
    [Id(6)] public string? MethodName { get; set; }
    [Id(7)] public string? ClassName { get; set; }
} 