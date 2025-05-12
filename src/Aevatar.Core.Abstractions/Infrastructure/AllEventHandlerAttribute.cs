// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class AllEventHandlerAttribute(int priority = 10, bool allowSelfHandling = false) : Attribute
{
    public int Priority { get; } = priority;
    public bool AllowSelfHandling { get; } = allowSelfHandling;
}