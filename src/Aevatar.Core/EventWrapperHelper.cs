using Aevatar.Core.Abstractions;

namespace Aevatar.Core;

/// <summary>
/// Utility methods for working with EventWrapper objects
/// </summary>
internal static class EventWrapperHelper
{
    /// <summary>
    /// Extracts a property from an EventWrapper using reflection
    /// </summary>
    public static T? GetProperty<T>(EventWrapperBase wrapper, string propertyName) where T : class
    {
        return wrapper.GetType().GetProperty(propertyName)?.GetValue(wrapper) as T;
    }
    
    /// <summary>
    /// Extracts common properties from an EventWrapper
    /// </summary>
    public static (EventBase eventType, string? eventId) ExtractProperties(EventWrapperBase wrapper)
    {
        var eventType = GetProperty<EventBase>(wrapper, nameof(EventWrapper<EventBase>.Event))!;
        var eventId = GetProperty<object>(wrapper, nameof(EventWrapper<EventBase>.EventId))?.ToString();
        
        return (eventType, eventId);
    }
} 