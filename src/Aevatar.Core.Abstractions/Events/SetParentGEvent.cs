// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SetParentStateLogEvent : StateLogEventBase
{
    [Id(0)] public GrainId Parent { get; set; }
}