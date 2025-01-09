// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class SubscribedEventListEvent : EventBase
{
    /// <summary>
    /// Key: GAgent Type.
    /// Value: Subscribed Event Types.
    /// </summary>
    [Id(0)] public required Dictionary<Type, List<Type>> Value { get; set; }

    [Id(1)] public required Type GAgentType { get; set; }
}