
using Microsoft.Extensions.Logging;
using Orleans.Providers;

using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace MessagingGAgent.Grains;

[GenerateSerializer]
public class DemoEvent : EventBase
{
    [Id(0)] public int Number { get; set; } = 0;
}


[GenerateSerializer]
public class DemoStateLogEvent : StateLogEventBase<DemoStateLogEvent>
{
    public int AddMe { get; set; } = 0;
}


public class DemoGState : BroadCastGState
{
    public int Count { get; set; } = 0;

    public void Apply(DemoStateLogEvent @event)
    {
        Count = Count + @event.AddMe;
    }
}
public interface IDemoGAgent : IBroadCastGAgent
{
    Task<int> GetCount();

    Task UnSub<T>() where T : EventBase;

    Task UnSubWithOutHandle<T>() where T : EventBase;

    Task PublishAsync<T>(GrainId grainId,T @event) where T : EventBase;
}

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class DemoGAgent : BroadCastGAgentBase<DemoGState, DemoStateLogEvent>, IDemoGAgent
{
    public async Task PublishAsync<T>(GrainId grainId,T @event) where T : EventBase
    {
        var grainIdString = grainId.ToString();
        var streamId = StreamId.Create(AevatarOptions!.StreamNamespace, grainIdString);
        var stream = StreamProvider.GetStream<EventWrapperBase>(streamId);
        var eventWrapper = new EventWrapper<T>(@event, Guid.NewGuid(), this.GetGrainId());
        await stream.OnNextAsync(eventWrapper);
    }
    
    private StreamSubscriptionHandle<EventWrapperBase>? _handle;
    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        _handle = await SubscribeBroadCastEventAsync<DemoEvent>("DemoScheduleGAgent", OnAddNumberEvent);

        await base.OnGAgentActivateAsync(cancellationToken);
    }
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("A demo counting agent");
    }

    [EventHandler]
    public async Task OnAddNumberEvent(DemoEvent @event)
    {
        RaiseEvent(new DemoStateLogEvent() { AddMe = @event.Number });
        Logger.LogInformation($"DemoGAgent received event {@event}");
        await ConfirmEvents();
    }

    public Task<int> GetCount()
    {
        Logger.LogInformation($"GetCount called, current count is {State.Count}");
        return Task.FromResult(State.Count);
    }

    public async Task UnSub<T>() where T : EventBase
    {
        Logger.LogInformation($"UnSub called");
        await UnSubscribeBroadCastAsync<T>("DemoScheduleGAgent", _handle!);
    }

    public async Task UnSubWithOutHandle<T>() where T : EventBase
    {
        Logger.LogInformation($"UnSub called");
        await UnSubscribeBroadCastAsync<T>("DemoScheduleGAgent");
    }

}

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class DemoScheduleGAgent : BroadCastGAgentBase<BroadCastGState, DemoStateLogEvent>, IBroadCastGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("A demo scbheduling agent");
    }
}