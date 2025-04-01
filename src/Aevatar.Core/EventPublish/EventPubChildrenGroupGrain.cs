using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.EventPublish;
using Aevatar.Core.Extensions;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

[GenerateSerializer]
public class EventPubChildrenGroupState : StateBase
{
    [Id(0)] public List<GrainId> Children { get; set; } = new();
    [Id(1)] public int ChildrenCount { get; set; }
}

[GenerateSerializer]
public class EventPubChildrenGroupStateLogEvent : StateLogEventBase<EventPubChildrenGroupStateLogEvent>;

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class EventPubChildrenGroupGrain : 
    JournaledGrain<EventPubChildrenGroupState, StateLogEventBase<EventPubChildrenGroupStateLogEvent>>,
    IEventPubChildrenGroupGrain
{
    private readonly IStreamProvider _streamProvider;

    public EventPubChildrenGroupGrain()
    {
        _streamProvider = this.GetStreamProvider(AevatarCoreConstants.StreamProvider);
    }

    public async Task DownwardsEventAsync(EventWrapperBase eventWrapper)
    {
        foreach (var eventPubGrain in State.Children.Select(grainId =>
                     GrainFactory.GetGrain<IEventPubGrain>(grainId.ToString())))
        {
            await eventPubGrain.PublishEventAsync(eventWrapper);
        }
    }

    public async Task UpwardsEventAsync(EventWrapperBase eventWrapper)
    {
        var stream = _streamProvider.GetEventWrapperBaseStream(this.GetPrimaryKeyString());
        await stream.OnNextAsync(eventWrapper);
    }

    public async Task AddChildAsync(GrainId childId)
    {
        RaiseEvent(new AddChildStateLogEvent { ChildId = childId });
        await ConfirmEvents();
    }

    public async Task RemoveChildAsync(GrainId childId)
    {
        RaiseEvent(new RemoveChildStateLogEvent { ChildId = childId });
        await ConfirmEvents();
    }

    public Task<List<GrainId>> GetChildrenAsync()
    {
        return Task.FromResult(State.Children);
    }

    public Task<int> GetChildrenCountAsync()
    {
        return Task.FromResult(State.ChildrenCount);
    }

    protected override void TransitionState(EventPubChildrenGroupState state, StateLogEventBase<EventPubChildrenGroupStateLogEvent> @event)
    {
        switch (@event)
        {
            case AddChildStateLogEvent addChildEvent:
                state.Children.Add(addChildEvent.ChildId);
                state.ChildrenCount++;
                break;
            case RemoveChildStateLogEvent removeChildEvent:
                state.Children.Remove(removeChildEvent.ChildId);
                state.ChildrenCount--;
                break;
        }

        base.TransitionState(state, @event);
    }

    [GenerateSerializer]
    public class AddChildStateLogEvent : StateLogEventBase<EventPubChildrenGroupStateLogEvent>
    {
        [Id(0)] public GrainId ChildId { get; set; }
    }

    [GenerateSerializer]
    public class RemoveChildStateLogEvent : StateLogEventBase<EventPubChildrenGroupStateLogEvent>
    {
        [Id(0)] public GrainId ChildId { get; set; }
    }
}