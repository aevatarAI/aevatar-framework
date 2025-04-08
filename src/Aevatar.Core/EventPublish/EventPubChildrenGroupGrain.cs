using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.EventPublish;
using Aevatar.Core.Extensions;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;
using Orleans.Concurrency;

namespace Aevatar.Core.EventPublish;

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
[Reentrant]
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
        foreach (var childGrainId in State.Children)
        {
            var stream = _streamProvider.GetEventWrapperBaseStream(childGrainId.ToString());
            await stream.OnNextAsync(eventWrapper);
        }
    }

    public async Task UpwardsEventAsync(EventWrapperBase eventWrapper)
    {
        var stream = _streamProvider.GetEventWrapperBaseStream(this.GetPrimaryKeyString());
        await stream.OnNextAsync(eventWrapper);
    }

    public async Task AddChildAsync(GrainId childGrainId)
    {
        RaiseEvent(new AddChildStateLogEvent { ChildId = childGrainId });
        await ConfirmEvents();
    }

    public async Task AddManyChildAsync(List<GrainId> childrenGrainIds)
    {
        base.RaiseEvent(new AddChildManyStateLogEvent
        {
            ChildrenIds = childrenGrainIds
        });
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

    protected override void TransitionState(EventPubChildrenGroupState state,
        StateLogEventBase<EventPubChildrenGroupStateLogEvent> @event)
    {
        switch (@event)
        {
            case AddChildStateLogEvent addChildEvent:
                state.Children.Add(addChildEvent.ChildId);
                state.ChildrenCount++;
                break;
            case AddChildManyStateLogEvent addChildManyEvent:
                state.Children.AddRange(addChildManyEvent.ChildrenIds);
                state.ChildrenCount += addChildManyEvent.ChildrenIds.Count;
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
        [Id(0)] public required GrainId ChildId { get; set; }
    }

    [GenerateSerializer]
    public class AddChildManyStateLogEvent : StateLogEventBase<EventPubChildrenGroupStateLogEvent>
    {
        [Id(0)] public required List<GrainId> ChildrenIds { get; set; }
    }

    [GenerateSerializer]
    public class RemoveChildStateLogEvent : StateLogEventBase<EventPubChildrenGroupStateLogEvent>
    {
        [Id(0)] public required GrainId ChildId { get; set; }
    }
}