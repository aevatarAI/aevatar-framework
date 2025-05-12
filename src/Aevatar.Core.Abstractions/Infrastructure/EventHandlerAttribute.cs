// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class EventHandlerAttribute(int priority = 100, bool allowSelfHandling = false) : Attribute
{
    public int Priority { get; } = priority;
    public bool AllowSelfHandling { get; } = allowSelfHandling;
}