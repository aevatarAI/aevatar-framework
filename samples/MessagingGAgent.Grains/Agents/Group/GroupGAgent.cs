using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace MessagingGAgent.Grains.Agents.Group;

public interface IGroupGAgent : IGAgent
{
}

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class GroupGAgent : GAgentBase<GroupAgentState, GroupStateLogEvent>, IGroupGAgent
{
    public GroupGAgent(ILogger<GroupGAgent> logger) : base(logger)
    {
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("An agent to inform other agents when a social event is published.");
    }

    protected override Task OnRegisterAgentAsync(Guid agentGuid)
    {
        ++State.RegisteredAgents;
        return Task.CompletedTask;
    }

    protected override Task OnUnregisterAgentAsync(Guid agentGuid)
    {
        --State.RegisteredAgents;
        return Task.CompletedTask;
    }
    
    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        State.RegisteredAgents = 0;
    }
}