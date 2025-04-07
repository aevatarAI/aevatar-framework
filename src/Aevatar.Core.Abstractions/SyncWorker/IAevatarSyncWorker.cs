using Orleans.Streams;
using Orleans.SyncWork;

namespace Aevatar.Core.Abstractions.SyncWorker;

public interface IAevatarSyncWorker<TRequest, TResponse> : ISyncWorker<TRequest, TResponse>, IGrainWithGuidKey
    where TRequest : EventBase
    where TResponse : EventBase
{
    Task SetLongRunTaskAsync(IAsyncStream<EventWrapperBase> callbackStream);
}