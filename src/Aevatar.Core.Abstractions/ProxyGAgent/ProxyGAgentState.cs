namespace Aevatar.Core.Abstractions.ProxyGAgent;

[GenerateSerializer]
public class ProxyGAgentState : StateBase
{
    [Id(0)] public byte[]? PluginCode { get; set; }
    [Id(1)] public Dictionary<string, object>? Database { get; set; }
}