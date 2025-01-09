// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class RemoveChildStateLogEvent : StateLogEventBase
{
    [Id(0)] public GrainId Child { get; set; }
}