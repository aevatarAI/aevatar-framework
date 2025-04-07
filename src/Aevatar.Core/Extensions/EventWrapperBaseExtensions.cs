using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Extensions;

public static class EventWrapperBaseExtensions
{
    public static GrainId GetGrainId(this EventWrapperBase eventWrapper)
    {
        return (GrainId)eventWrapper.GetType().GetProperty(nameof(EventWrapper<EventBase>.GrainId))
            ?.GetValue(eventWrapper)!;
    }

    public static GrainId GetPublisherGrainId(this EventWrapperBase eventWrapper)
    {
        return (GrainId)eventWrapper.GetType().GetProperty(nameof(EventWrapper<EventBase>.PublisherGrainId))
            ?.GetValue(eventWrapper)!;
    }

    public static Guid? GetCorrelationId(this EventWrapperBase eventWrapper)
    {
        return (Guid?)eventWrapper.GetType().GetProperty(nameof(EventWrapper<EventBase>.CorrelationId))
            ?.GetValue(eventWrapper)!;
    }

    public static EventBase GetEvent(this EventWrapperBase eventWrapper)
    {
        return (EventBase)eventWrapper.GetType().GetProperty(nameof(EventWrapper<EventBase>.Event))
            ?.GetValue(eventWrapper)!;
    }

    public static Guid GetEventId(this EventWrapperBase eventWrapper)
    {
        return (Guid)eventWrapper.GetType().GetProperty(nameof(EventWrapper<EventBase>.EventId))
            ?.GetValue(eventWrapper)!;
    }
}