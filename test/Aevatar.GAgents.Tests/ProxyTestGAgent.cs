using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestInitializeDtos;
using Aevatar.Plugins.Test;
using Aevatar.ProxyGAgent.Sdk;
using Microsoft.Extensions.Logging;

namespace Aevatar.GAgents.Tests;

[GenerateSerializer]
public class ProxyTestGAgentState : StateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

[GenerateSerializer]
public class ProxyTestStateLogEvent : StateLogEventBase<ProxyTestStateLogEvent>
{
    [Id(0)] public Guid Id { get; set; }
}

[GAgent("proxyTest")]
public class ProxyTestGAgent : GAgentBase<ProxyTestGAgentState, ProxyTestStateLogEvent>
{
    public ProxyTestGAgent(ILogger<ProxyTestGAgent> logger) : base(logger)
    {
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a proxy test GAgent");
    }

    public async Task HandleEventAsync(ProxyGAgentEvent eventData)
    {
        if (State.Content.IsNullOrEmpty())
        {
            State.Content = [];
        }

        var data = eventData.EventData["Greeting"].ToString()!;
        State.Content.Add(data);
    }
}