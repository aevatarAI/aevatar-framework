using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.SyncWorker;
using Aevatar.Core.Extensions;
using Microsoft.Extensions.Logging;
using Orleans.SyncWork;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
{
    protected async Task CreateLongRunTaskAsync<TRequest, TResponse>(TRequest request)
        where TRequest : EventBase
        where TResponse : EventBase
    {
        try
        {
            var syncWorker = GrainFactory.GetGrain<IAevatarSyncWorker<TRequest, TResponse>>(Guid.NewGuid());
            await syncWorker.SetLongRunTaskAsync(StreamProvider.GetEventWrapperBaseStream(GrainId));
            await syncWorker.StartWorkAndPollUntilResult(request);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error creating long run task: {ex.Message}");
            throw;
        }
    }
}