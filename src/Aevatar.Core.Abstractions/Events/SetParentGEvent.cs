// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SetParentGEvent : StateLogEventBase
{
    [Id(0)] public GrainId Parent { get; set; }
}