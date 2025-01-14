using Aevatar.Core.Abstractions;

namespace Aevatar.ProxyGAgent.Sdk;

[GenerateSerializer]
public class ProxyGAgentEvent : EventBase
{
    [Id(0)] public Dictionary<string, object> EventData { get; set; }
}