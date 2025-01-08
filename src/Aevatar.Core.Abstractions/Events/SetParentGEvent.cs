// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SetParentGEvent : GEventBase
{
    [Id(0)] public GrainId Parent { get; set; }
}