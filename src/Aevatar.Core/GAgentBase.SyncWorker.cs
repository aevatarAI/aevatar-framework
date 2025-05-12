using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.SyncWorker;
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
            var grainId = $"{typeof(TRequest).Name}_to_{typeof(TResponse).Name}/{Guid.NewGuid()}";
            var syncWorker = GrainFactory.GetGrain<IAevatarSyncWorker<TRequest, TResponse>>(grainId);
            await syncWorker.SetLongRunTaskAsync(GetEventBaseStream(GrainId));
            await syncWorker.Start(request);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error creating long run task: {ex.Message}");
            throw;
        }
    }
}