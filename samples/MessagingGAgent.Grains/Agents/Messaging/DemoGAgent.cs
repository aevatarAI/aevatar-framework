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
public class DemoMutiplyEvent : EventBase
{
    [Id(0)] public int Number { get; set; } = 5;
}

[GenerateSerializer]
public class DemoDivideEvent : EventBase
{
    [Id(0)] public int Number { get; set; } = 2;
}

[GenerateSerializer]
public enum OperationType
{
    Add,
    Multiply,
    Divide
}

[GenerateSerializer]
public class DemoStateLogEvent : StateLogEventBase<DemoStateLogEvent>
{
    [Id(0)] public int Number { get; set; } = 0;
    [Id(1)] public OperationType Operation { get; set; } = OperationType.Add;
}


public class DemoGState : BroadCastGState
{
    public int Count { get; set; } = 0;

    public void Apply(DemoStateLogEvent @event)
    {
        switch (@event.Operation)
        {
            case OperationType.Add:
                Count = Count + @event.Number;
                break;
            case OperationType.Multiply:
                Count = Count * @event.Number;
                break;
            case OperationType.Divide:
                if (@event.Number != 0) // Avoid divide by zero
                {
                    Count = Count / @event.Number;
                }
                break;
            default:
                Count = Count + @event.Number; // Default to addition
                break;
        }
    }
}

public interface IDemoBatchSubGAgent : IDemoGAgent
{

}

public interface IDemoGAgent : IBroadCastGAgent
{
    Task<int> GetCount();

    Task UnSub<T>() where T : EventBase;

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
    
    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken = default)
    {

        await SubscribeBroadCastEventAsync<DemoEvent>("DemoScheduleGAgent", OnAddNumberEvent);

        await base.OnGAgentActivateAsync(cancellationToken);
    }
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("A demo counting agent");
    }

    [EventHandler]
    public async Task OnAddNumberEvent(DemoEvent @event)
    {
        // Add the Number value to the count
        RaiseEvent(new DemoStateLogEvent { Number = @event.Number, Operation = OperationType.Add });
        Logger.LogInformation($"DemoGAgent received add event: {nameof(DemoEvent)} with value {@event.Number}");
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task OnMutiplyNumberEvent(DemoMutiplyEvent @event)
    {
        RaiseEvent(new DemoStateLogEvent { Number = @event.Number, Operation = OperationType.Multiply });
        Logger.LogInformation($"DemoGAgent received multiply event: {nameof(DemoMutiplyEvent)} with value {@event.Number}");
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task OnDivideNumberEvent(DemoDivideEvent @event)
    {
        RaiseEvent(new DemoStateLogEvent { Number = @event.Number, Operation = OperationType.Divide });
        Logger.LogInformation($"DemoGAgent received divide event: {nameof(DemoDivideEvent)} with value {@event.Number}");
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
        await UnSubscribeBroadCastAsync<T>("DemoScheduleGAgent");
    }
}


[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class DemoBatchSubGAgent : BroadCastGAgentBase<DemoGState, DemoStateLogEvent>, IDemoBatchSubGAgent
{
    private Dictionary<string, StreamSubscriptionHandle<EventWrapperBase>> _handles = new();
    
    public async Task PublishAsync<T>(GrainId grainId,T @event) where T : EventBase
    {
        var grainIdString = grainId.ToString();
        var streamId = StreamId.Create(AevatarOptions!.StreamNamespace, grainIdString);
        var stream = StreamProvider.GetStream<EventWrapperBase>(streamId);
        var eventWrapper = new EventWrapper<T>(@event, Guid.NewGuid(), this.GetGrainId());
        await stream.OnNextAsync(eventWrapper);
    }
    
    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken = default)
    {
        // Multiple event types with fluent API
        await StartBatchSubscriptionAsync();
        await AddSubscriptionAsync<DemoEvent>("DemoScheduleGAgent", OnAddNumberEvent);
        await AddSubscriptionAsync<DemoMutiplyEvent>("DemoScheduleGAgent", OnMutiplyNumberEvent);
        await AddSubscriptionAsync<DemoDivideEvent>("DemoScheduleGAgent", OnDivideNumberEvent);        
        _handles = await SaveBatchSubscriptionsAsync();

        await base.OnGAgentActivateAsync(cancellationToken);
    }
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("A demo counting agent");
    }

    [EventHandler]
    public async Task OnAddNumberEvent(DemoEvent @event)
    {
        // Add the Number value to the count
        RaiseEvent(new DemoStateLogEvent { Number = @event.Number, Operation = OperationType.Add });
        Logger.LogInformation($"DemoGAgent received add event: {nameof(DemoEvent)} with value {@event.Number}");
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task OnMutiplyNumberEvent(DemoMutiplyEvent @event)
    {
        RaiseEvent(new DemoStateLogEvent { Number = @event.Number, Operation = OperationType.Multiply });
        Logger.LogInformation($"DemoGAgent received multiply event: {nameof(DemoMutiplyEvent)} with value {@event.Number}");
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task OnDivideNumberEvent(DemoDivideEvent @event)
    {
        RaiseEvent(new DemoStateLogEvent { Number = @event.Number, Operation = OperationType.Divide });
        Logger.LogInformation($"DemoGAgent received divide event: {nameof(DemoDivideEvent)} with value {@event.Number}");
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