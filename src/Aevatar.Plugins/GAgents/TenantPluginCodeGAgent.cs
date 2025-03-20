using Aevatar.Core;
using Aevatar.Core.Abstractions;

namespace Aevatar.Plugins.GAgents;

[GenerateSerializer]
public class TenantPluginCodeGAgentState : StateBase
{
    [Id(0)] public List<Guid> CodeStorageGuids { get; set; } = [];
}

[GenerateSerializer]
public class TenantPluginStateLogEvent : StateLogEventBase<TenantPluginStateLogEvent>
{
    [Id(0)] public override Guid Id { get; set; } = Guid.NewGuid();
}

public interface ITenantPluginCodeGAgent : IStateGAgent<TenantPluginCodeGAgentState>
{
    Task AddPluginAsync(Guid pluginCodeId);
    Task RemovePluginAsync(Guid pluginCodeId);
    Task AddPluginsAsync(IEnumerable<Guid> pluginCodeIds);
}

[GAgent]
public class TenantPluginCodeGAgent
    : GAgentBase<TenantPluginCodeGAgentState, TenantPluginStateLogEvent>, ITenantPluginCodeGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used to store plugin list for a specific tenant.");
    }

    protected override void GAgentTransitionState(TenantPluginCodeGAgentState state,
        StateLogEventBase<TenantPluginStateLogEvent> @event)
    {
        switch (@event)
        {
            case AddPluginCodeStateLogEvent addPluginCodeStateLogEvent:
                if (State.CodeStorageGuids.IsNullOrEmpty())
                    State.CodeStorageGuids = [];
                State.CodeStorageGuids.AddRange(addPluginCodeStateLogEvent.PluginCodeIds);
                break;
            case RemovePluginCodeStateLogEvent removePluginCodeStateLogEvent:
                if (State.CodeStorageGuids.Contains(removePluginCodeStateLogEvent.PluginCodeId))
                    State.CodeStorageGuids.Remove(removePluginCodeStateLogEvent.PluginCodeId);
                break;
        }

        base.GAgentTransitionState(state, @event);
    }

    public async Task RemovePluginAsync(Guid pluginCodeId)
    {
        RaiseEvent(new RemovePluginCodeStateLogEvent
        {
            PluginCodeId = pluginCodeId
        });
        await ConfirmEvents();
    }

    public async Task AddPluginsAsync(IEnumerable<Guid> pluginCodeIds)
    {
        RaiseEvent(new AddPluginCodeStateLogEvent
        {
            PluginCodeIds = pluginCodeIds.ToList()
        });
        await ConfirmEvents();
    }

    public async Task AddPluginAsync(Guid pluginCodeId)
    {
        RaiseEvent(new AddPluginCodeStateLogEvent
        {
            PluginCodeIds = [pluginCodeId]
        });
        await ConfirmEvents();
    }


    [GenerateSerializer]
    public class AddPluginCodeStateLogEvent : TenantPluginStateLogEvent
    {
        [Id(0)] public required List<Guid> PluginCodeIds { get; set; }
    }

    [GenerateSerializer]
    public class RemovePluginCodeStateLogEvent : TenantPluginStateLogEvent
    {
        [Id(0)] public required Guid PluginCodeId { get; set; }
    }
}