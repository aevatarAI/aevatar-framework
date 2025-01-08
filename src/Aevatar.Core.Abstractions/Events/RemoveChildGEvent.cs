// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class RemoveChildGEvent : GEventBase
{
    [Id(0)] public GrainId Child { get; set; }
}