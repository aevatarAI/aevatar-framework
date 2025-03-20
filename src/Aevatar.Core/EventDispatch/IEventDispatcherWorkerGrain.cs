using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core.EventDispatch;

public interface IEventDispatcherWorkerGrain : IGrainWithGuidKey
{
    Task ExecuteDispatchAsync<T>(IReadOnlyList<IAsyncStream<EventWrapperBase>> streams, EventWrapper<T> eventWrapper)
        where T : EventBase;
}