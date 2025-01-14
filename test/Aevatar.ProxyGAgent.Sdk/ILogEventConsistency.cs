using Aevatar.Core.Abstractions.ProxyGAgent;

namespace Aevatar.ProxyGAgent.Sdk;

public interface ILogEventConsistency
{
    ProxyGAgentState State { set; }
    Task Apply(ProxyStateLogEvent eventData);
}