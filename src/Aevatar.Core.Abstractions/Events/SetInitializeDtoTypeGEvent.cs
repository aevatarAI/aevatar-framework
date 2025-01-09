namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SetInitializeDtoTypeGEvent : StateLogEventBase
{
    [Id(0)] public Type InitializeDtoType { get; set; }
}