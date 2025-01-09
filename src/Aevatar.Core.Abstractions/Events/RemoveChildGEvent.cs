// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class RemoveChildGEvent : StateLogEventBase
{
    [Id(0)] public GrainId Child { get; set; }
}