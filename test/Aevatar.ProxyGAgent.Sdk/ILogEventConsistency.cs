using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.ProxyGAgent;

namespace Aevatar.ProxyGAgent.Sdk;

public interface ILogEventConsistency
{
    ProxyGAgentState State { set; }
    void Apply(ProxyStateLogEvent eventData);
}