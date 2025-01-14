namespace Aevatar.Core.Abstractions.ProxyGAgent;

[GenerateSerializer]
public class ProxyGAgentInitialization : InitializationEventBase
{
    [Id(0)] public byte[] PluginCode { get; set; }
}