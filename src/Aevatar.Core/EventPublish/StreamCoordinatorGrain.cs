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

[GenerateSerializer]
public class StreamCoordinatorState : StateBase
{
    /// <summary>
    /// Group index -> Children count.
    /// </summary>
    [Id(1)] public Dictionary<int, int> GroupChildrenCount { get; set; } = new();
    [Id(2)] public GrainId Parent { get; set; }
}

[GenerateSerializer]
public class StreamCoordinatorStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>;

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class StreamCoordinatorGrain :
    JournaledGrain<StreamCoordinatorState, StateLogEventBase<StreamCoordinatorStateLogEvent>>,
    IStreamCoordinatorGrain
{
    private readonly ILogger<StreamCoordinatorGrain> _logger;
    private readonly IStreamProvider _streamProvider;
    private readonly AevatarOptions _aevatarOptions;

    public StreamCoordinatorGrain(ILogger<StreamCoordinatorGrain> logger)
    {
        _logger = logger;
        _streamProvider = this.GetStreamProvider(AevatarCoreConstants.StreamProvider);
        _aevatarOptions = ServiceProvider.GetRequiredService<IOptions<AevatarOptions>>().Value;
    }

    public async Task<bool> SetParentAsync(GrainId parentGrainId)
    {
        if (State.Parent == parentGrainId)
        {
            return false;
        }
        RaiseEvent(new SetParentStateLogEvent{ParentGrainId = parentGrainId});
        await ConfirmEvents();
        return true;
    }

    public Task<GrainId> GetParentAsync()
    {
        return Task.FromResult(State.Parent);
    }

    public async Task RegisterChildAsync(GrainId childGrainId)
    {
        var groupIndex = State.GroupChildrenCount
            .FirstOrDefault(kvp => kvp.Value <= AevatarGAgentConstants.MaxChildrenPerGroup).Key;
        var childGroupGrain = GetChildGroupGrain(groupIndex);
        await childGroupGrain.AddChildAsync(childGrainId);
        var childrenCount = await childGroupGrain.GetChildrenCountAsync();
        RaiseEvent(new UpdateGroupChildrenCountStateLogEvent { GroupIndex = groupIndex, ChildrenCount = childrenCount });
        RaiseEvent(new RegisterChildStateLogEvent { ChildGroupGrainId = childGrainId, GroupIndex = groupIndex });
        await SubscribeToChildAsync(childGrainId, groupIndex);
        await ConfirmEvents();
    }

    public async Task UnregisterChildAsync(GrainId childGrainId)
    {
        foreach (var group in State.GroupChildrenCount)
        {
            var childGroupGrain =
                GrainFactory.GetGrain<IEventPubChildrenGroupGrain>(group.Key, this.GetPrimaryKeyString());

            var children = await childGroupGrain.GetChildrenAsync();
            if (children.Contains(childGrainId))
            {
                await childGroupGrain.RemoveChildAsync(childGrainId);
                RaiseEvent(new UnregisterChildStateLogEvent { ChildGrainId = childGrainId, GroupIndex = group.Key });
                await ConfirmEvents();

                await UnsubscribeFromChildAsync(childGrainId, group.Key);
                break;
            }
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
        if (State.Parent == default)
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
        var selfEventPubGrain = GrainFactory.GetGrain<IEventPubGrain>($"{this.GetPrimaryKeyString()}");
        await selfEventPubGrain.PublishEventAsync(eventWrapper);

        // Parent handling
        if (State.Parent != default)
        {
            // var parentEventPubGrain = GrainFactory.GetGrain<IEventPubGrain>(State.Parent.ToString());
            // await parentEventPubGrain.PublishEventAsync(eventWrapper);
            // To avoid too many producers on parent's corresponding PubSubRendezvousGrain state.
            var parentStream = _streamProvider.GetEventWrapperBaseStream(State.Parent);
            await parentStream.OnNextAsync(eventWrapper);
        }
    }

    private IEventPubChildrenGroupGrain GetChildGroupGrain(int groupIndex)
    {
        return GrainFactory.GetGrain<IEventPubChildrenGroupGrain>(groupIndex, this.GetPrimaryKeyString());
    }

    private async Task SubscribeToChildAsync(GrainId childId, int groupIndex)
    {

    }

    private async Task UnsubscribeFromChildAsync(GrainId childId, int groupIndex)
    {
    }

    protected override void TransitionState(StreamCoordinatorState state,
        StateLogEventBase<StreamCoordinatorStateLogEvent> @event)
    {
        switch (@event)
        {
            case SetParentStateLogEvent setParentEvent:
                State.Parent = setParentEvent.ParentGrainId;
                break;
            case UpdateGroupChildrenCountStateLogEvent updateEvent:
                State.GroupChildrenCount[updateEvent.GroupIndex] = updateEvent.ChildrenCount;
                if (updateEvent.ChildrenCount == AevatarGAgentConstants.MaxChildrenPerGroup)
                {
                    State.GroupChildrenCount[updateEvent.GroupIndex + 1] = 0;
                }
                break;
            case RegisterChildStateLogEvent registerEvent:
                break;
            case UnregisterChildStateLogEvent unregisterEvent:
                if (state.GroupChildrenCount.ContainsKey(unregisterEvent.GroupIndex))
                {
                    state.GroupChildrenCount[unregisterEvent.GroupIndex] -= 1;
                }

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
    public class RegisterChildStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public GrainId ChildGroupGrainId { get; set; }
        [Id(1)] public int GroupIndex { get; set; }
    }

    [GenerateSerializer]
    public class UnregisterChildStateLogEvent : StateLogEventBase<StreamCoordinatorStateLogEvent>
    {
        [Id(0)] public GrainId ChildGrainId { get; set; }
        [Id(1)] public int GroupIndex { get; set; }
    }
}