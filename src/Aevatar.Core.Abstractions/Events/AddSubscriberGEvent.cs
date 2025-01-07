// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class AddSubscriberGEvent : GEventBase
{
    [Id(0)] public GrainId Subscriber { get; set; }
}