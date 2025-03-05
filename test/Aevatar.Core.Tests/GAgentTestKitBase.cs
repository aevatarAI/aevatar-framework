using System.Linq.Expressions;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.TestKit;
using Aevatar.TestKit.Extensions;

namespace Aevatar.Core.Tests;

public abstract class GAgentTestKitBase : TestKitBase<AevatarTestKitSilo>
{
    protected async Task<PublishingGAgent> CreatePublishingGAgentAsync(params IGAgent[] gAgentsToPublish)
    {
        var publishingGAgent = await Silo.CreateGrainAsync<PublishingGAgent>(Guid.NewGuid());
        Silo.AddProbe<IPublishingGAgent>(_ => publishingGAgent);
        foreach (var gAgent in gAgentsToPublish)
        {
            await publishingGAgent.RegisterAsync(gAgent);
        }

        return publishingGAgent;
    }

    protected async Task<GroupGAgent> CreateGroupGAgentAsync(params IGAgent[] gAgentsToRegister)
    {
        var groupGAgent = await Silo.CreateGrainAsync<GroupGAgent>(Guid.NewGuid());
        foreach (var gAgent in gAgentsToRegister)
        {
            await groupGAgent.RegisterAsync(gAgent);
        }

        return groupGAgent;
    }

    protected void AddProbesByGrainId(params IGAgent?[] gAgents)
    {
        foreach (var gAgent in gAgents)
        {
            Silo.AddProbe(gAgent.GetGrainId(), gAgent);
        }
    }

    protected void AddProbesByIdSpan(params IGAgent?[] gAgents)
    {
        var parameter = Expression.Parameter(typeof(IdSpan), "idSpan");
        Expression body = Expression.Constant(null, typeof(IGAgent));

        foreach (var gAgent in gAgents)
        {
            var primaryKey = gAgent.GetPrimaryKey();
            var grainId = GrainIdKeyExtensions.CreateGuidKey(primaryKey);
            var condition = Expression.Equal(parameter, Expression.Constant(grainId));
            var result = Expression.Constant(gAgent, typeof(IGAgent));
            body = Expression.Condition(condition, result, body);
        }

        var lambda = Expression.Lambda<Func<IdSpan, IGAgent>>(body, parameter).Compile();
        Silo.AddProbe(lambda);
    }
}