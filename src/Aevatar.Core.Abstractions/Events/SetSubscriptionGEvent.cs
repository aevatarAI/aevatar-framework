// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SetSubscriptionGEvent : GEventBase
{
    [Id(0)] public GrainId Subscription { get; set; }
}