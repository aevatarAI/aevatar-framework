using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core.EventDispatch;

public class EventDispatcherWorkerGrain : Grain, IEventDispatcherWorkerGrain
{
    public async Task ExecuteDispatchAsync<T>(IReadOnlyList<IAsyncStream<EventWrapperBase>> streams,
        EventWrapper<T> eventWrapper) where T : EventBase
    {
        if (streams.Count <= AevatarGAgentConstants.EventDispatcherMaxBatchSize)
        {
            var tasks = streams.Select(s => s.GetAllSubscriptionHandles());
            await Task.WhenAll(tasks);
        }
        else
        {
            var batches = streams
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / AevatarGAgentConstants.EventDispatcherMaxBatchSize)
                .Select(g => g.Select(x => x.Value).ToList())
                .ToList();
            var subTasks = batches.Select(stream =>
            {
                var worker = GrainFactory.GetGrain<IEventDispatcherWorkerGrain>(
                    Guid.NewGuid()
                );
                return worker.ExecuteDispatchAsync(stream, eventWrapper);
            });

            await Task.WhenAll(subTasks);
        }
    }
}