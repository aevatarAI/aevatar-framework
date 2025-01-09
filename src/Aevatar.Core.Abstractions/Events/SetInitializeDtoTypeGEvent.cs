namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SetInitializeDtoTypeStateLogEvent : StateLogEventBase
{
    [Id(0)] public required Type InitializeDtoType { get; set; }
}