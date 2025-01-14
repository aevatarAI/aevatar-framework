namespace Aevatar.Core.Abstractions.ProxyGAgent;

[GenerateSerializer]
public class ProxyStateLogEvent : StateLogEventBase<ProxyStateLogEvent>
{
    [Id(0)] public Dictionary<string, object> Data { get; set; }
}