namespace Aevatar.Core.Abstractions.EventPublish;

public interface IEventPubGrain : IGrainWithStringKey
{
    Task PublishEventAsync(EventWrapperBase eventWrapper);
}