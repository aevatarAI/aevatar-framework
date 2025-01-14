namespace Aevatar.Core.Abstractions.ProxyGAgent;

[GenerateSerializer]
public class ProxyGAgentState : StateBase
{
    [Id(0)] public byte[] EventHandlerCode { get; set; }
    [Id(1)] public byte[] TransitionStateCode { get; set; }
    [Id(2)] public Dictionary<string, object> Database { get; set; }
}