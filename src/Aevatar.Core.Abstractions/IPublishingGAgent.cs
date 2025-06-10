namespace Aevatar.Core.Abstractions;

public interface IPublishingGAgent : IGAgent
{
    Task PublishEventAsync<T>(T @event, params IGAgent[] agents) where T : EventBase;
}