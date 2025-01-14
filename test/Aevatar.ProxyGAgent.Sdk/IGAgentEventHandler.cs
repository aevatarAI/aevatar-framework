using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.ProxyGAgent;

namespace Aevatar.ProxyGAgent.Sdk;

public interface IGAgentEventHandler<in T> where T : EventBase
{
    Task<EventHandleResult> HandleEventAsync(T eventBase);
}

public class EventHandleResult
{
    public List<ProxyStateLogEvent> StateLogEventList { get; set; }
    public List<EventBase> GAgentEventBase { get; set; }
}