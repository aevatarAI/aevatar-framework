using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.ProxyGAgent;
using Aevatar.ProxyGAgent.Sdk;

namespace Aevatar.Plugins.Test;

public class TestLogEventConsistency : ILogEventConsistency
{
    public ProxyGAgentState State { get; set; }
    public void Apply(ProxyStateLogEvent eventData)
    {
        State.Database ??= new Dictionary<string, object>();
        State.Database["Test"] = eventData.Data["Test"];
    }
}