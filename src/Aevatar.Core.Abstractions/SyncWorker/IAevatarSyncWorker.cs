using Orleans.Streams;
using Orleans.SyncWork;

namespace Aevatar.Core.Abstractions.SyncWorker;

public interface IAevatarSyncWorker<in TRequest, TResponse> : ISyncWorker<TRequest, TResponse>, IGrainWithStringKey
    where TRequest : EventBase
    where TResponse : EventBase
{
    Task SetLongRunTaskAsync(IAsyncStream<EventWrapperBase> callbackStream);
}