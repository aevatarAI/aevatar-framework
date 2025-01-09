// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class RequestAllSubscriptionsEvent : EventWithResponseBase<SubscribedEventListEvent>
{
    [Id(0)] public Type? RequestFromGAgentType { get; set; }
}