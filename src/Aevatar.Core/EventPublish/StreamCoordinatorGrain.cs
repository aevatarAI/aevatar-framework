using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.EventPublish;
using Aevatar.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;
using Orleans.Concurrency;

namespace Aevatar.Core.EventPublish;

[GenerateSerializer]
public class StreamCoordinatorState : StateBase
{
    /// <summary>
    /// Group index -> Children count.
    /// </summary>
    [Id(1)]
    public Dictionary<int, int> GroupChildrenCount { get; set; } = new();

    [Id(2)] public GrainId ParentGrainId { get; set; }
    [Id(3)] public int GroupIndex { get; set; }
}

[GenerateSerializer]
public class StreamCoordinatorStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>;

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
[Reentrant]
public class StreamCoordinatorGrain :
    JournaledGrain<StreamCoordinatorState, StateLogEventBase<StreamCoordinatorStateLogEvent>>,
    IStreamCoordinatorGrain
{
    private readonly ILogger<StreamCoordinatorGrain> _logger;
    private readonly IStreamProvider _streamProvider;

    public StreamCoordinatorGrain(ILogger<StreamCoordinatorGrain> logger)
    {
        _logger = logger;
        _streamProvider = this.GetStreamProvider(AevatarCoreConstants.StreamProvider);
    }

    public async Task<bool> SetParentAsync(GrainId parentGrainId)
    {
        if (State.ParentGrainId == parentGrainId)
        {
            return false;
        }
        RaiseEvent(new SetParentStateLogEvent{ParentGrainId = parentGrainId});
        await ConfirmEvents();
        return true;
    }

    public async Task ClearParentAsync(GrainId parentGrainId)
    {
        RaiseEvent(new ClearParentStateLogEvent
        {
            Parent = parentGrainId
        });
        await ConfirmEvents();
    }

    public async Task SetGroupIndexAsync(int groupIndex)
    {
        RaiseEvent(new SetGroupIndexStateLogEvent
        {
            GroupIndex = groupIndex
        });
        await ConfirmEvents();
    }

    public Task<int> GetGroupIndexAsync()
    {
        return Task.FromResult(State.GroupIndex);
    }

    public Task<GrainId> GetParentAsync()
    {
        return Task.FromResult(State.ParentGrainId);
    }

    public async Task<int> RegisterChildAsync(GrainId childGrainId)
    {
        var groupIndex = State.GroupChildrenCount
            .FirstOrDefault(kvp => kvp.Value <= AevatarGAgentConstants.MaxChildrenPerGroup).Key;
        var childGroupGrain = GetChildGroupGrain(groupIndex);
        var childrenCount = await childGroupGrain.GetChildrenCountAsync();
        await childGroupGrain.AddChildAsync(childGrainId);
        RaiseEvent(new UpdateGroupChildrenCountStateLogEvent { GroupIndex = groupIndex, ChildrenCount = childrenCount + 1 });
        await ConfirmEvents();
        return groupIndex;
    }

    public async Task<int> RegisterManyChildAsync(List<GrainId> childrenGrainIds)
    {
        var count = childrenGrainIds.Count;
        var groupIndex = State.GroupChildrenCount
            .FirstOrDefault(kvp => kvp.Value <= AevatarGAgentConstants.MaxChildrenPerGroup - count).Key;
        var childGroupGrain = GetChildGroupGrain(groupIndex);
        var childrenCount = await childGroupGrain.GetChildrenCountAsync();
        await childGroupGrain.AddManyChildAsync(childrenGrainIds);
        RaiseEvent(new UpdateGroupChildrenCountStateLogEvent { GroupIndex = groupIndex, ChildrenCount = childrenCount + count });
        await ConfirmEvents();
        return groupIndex;
    }

    public async Task UnregisterChildAsync(GrainId childGrainId)
    {
        var childStreamCoordinator = GrainFactory.GetGrain<IStreamCoordinatorGrain>(childGrainId.ToString());
        await childStreamCoordinator.ClearParentAsync(this.GetGrainId());
        var groupIndex = await childStreamCoordinator.GetGroupIndexAsync();
        var childGroupGrain =
            GrainFactory.GetGrain<IEventPubChildrenGroupGrain>(groupIndex, this.GetPrimaryKeyString());
        var children = await childGroupGrain.GetChildrenAsync();
        if (children.Contains(childGrainId))
        {
            await childGroupGrain.RemoveChildAsync(childGrainId);
            RaiseEvent(new UnregisterChildStateLogEvent { ChildGrainId = childGrainId, GroupIndex = groupIndex });
            await ConfirmEvents();
        }
        else
        {
            _logger.LogError($"Not found child {childGrainId} in group {groupIndex}.");
        }
    }

    public async Task<List<GrainId>> GetChildrenAsync()
    {
        var allChildren = new List<GrainId>();
        foreach (var group in State.GroupChildrenCount)
        {
            var childGroupGrain =
                GrainFactory.GetGrain<IEventPubChildrenGroupGrain>(group.Key, this.GetPrimaryKeyString());
            var children = await childGroupGrain.GetChildrenAsync();
            allChildren.AddRange(children);
        }

        return allChildren;
    }

    public async Task PublishEventAsync(EventWrapperBase eventWrapper)
    {
        if (State.ParentGrainId == default)
        {
            _logger.LogInformation(
                "Event is the first time appeared to silo: {@Event}", eventWrapper);
            await DownwardsEventAsync(eventWrapper);
        }
        else
        {
            _logger.LogInformation(
                "{GrainId} is publishing event upwards: {EventJson}", this.GetPrimaryKeyString(),
                JsonConvert.SerializeObject(eventWrapper));
            await UpwardsEventAsync(eventWrapper);
        }
    }

    public async Task DownwardsEventAsync(EventWrapperBase eventWrapper)
    {
        foreach (var (groupIndex, _) in State.GroupChildrenCount)
        {
            var childrenGroupGrain = GetChildGroupGrain(groupIndex);
            await childrenGroupGrain.DownwardsEventAsync(eventWrapper);
        }
    }

    public async Task UpwardsEventAsync(EventWrapperBase eventWrapper)
    {
        // Try self handling
        if (eventWrapper.GetPublisherGrainId().ToString() != this.GetPrimaryKeyString())
        {
            var selfStream = _streamProvider.GetEventWrapperBaseStream(this.GetPrimaryKeyString());
            await selfStream.OnNextAsync(eventWrapper);
        }

        // Parent handling
        if (State.ParentGrainId != default)
        {
            var parentStream = _streamProvider.GetEventWrapperBaseStream(State.ParentGrainId);
            await parentStream.OnNextAsync(eventWrapper);
        }
    }

    private IEventPubChildrenGroupGrain GetChildGroupGrain(int groupIndex)
    {
        return GrainFactory.GetGrain<IEventPubChildrenGroupGrain>(groupIndex, this.GetPrimaryKeyString());
    }

    protected override void TransitionState(StreamCoordinatorState state,
        StateLogEventBase<StreamCoordinatorStateLogEvent> @event)
    {
        switch (@event)
        {
            case SetParentStateLogEvent setParentEvent:
                State.ParentGrainId = setParentEvent.ParentGrainId;
                break;
            case ClearParentStateLogEvent clearParentStateLogEvent:
                if (clearParentStateLogEvent.Parent.ToString().Contains(State.ParentGrainId.ToString()))
                    State.ParentGrainId = default;
                break;
            case SetGroupIndexStateLogEvent setGroupIndexStateLogEvent:
                State.GroupIndex = setGroupIndexStateLogEvent.GroupIndex;
                break;
            case UpdateGroupChildrenCountStateLogEvent updateEvent:
                State.GroupChildrenCount[updateEvent.GroupIndex] = updateEvent.ChildrenCount;
                if (updateEvent.ChildrenCount == AevatarGAgentConstants.MaxChildrenPerGroup)
                    State.GroupChildrenCount[updateEvent.GroupIndex + 1] = 0;
                break;
            case UnregisterChildStateLogEvent unregisterEvent:
                if (state.GroupChildrenCount.ContainsKey(unregisterEvent.GroupIndex))
                    state.GroupChildrenCount[unregisterEvent.GroupIndex] -= 1;
                break;
        }

        base.TransitionState(state, @event);
    }
    
    [GenerateSerializer]
    public class SetParentStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public GrainId ParentGrainId { get; set; }
    }
    
    [GenerateSerializer]
    public class UpdateGroupChildrenCountStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public int GroupIndex { get; set; }
        [Id(1)] public int ChildrenCount { get; set; }
    }

    [GenerateSerializer]
    public class UnregisterChildStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public GrainId ChildGrainId { get; set; }
        [Id(1)] public int GroupIndex { get; set; }
    }
    
    [GenerateSerializer]
    public class ClearParentStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public GrainId Parent { get; set; }
    }
    
    [GenerateSerializer]
    public class SetGroupIndexStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public int GroupIndex { get; set; }
    }
}