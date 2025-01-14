namespace Aevatar.Core.Abstractions.ProxyGAgent;

[GenerateSerializer]
public class ProxyGAgentInitialization : InitializationEventBase
{
    [Id(0)] public byte[] EventHandlerCode { get; set; }
    [Id(1)] public byte[] TransitionStateCode { get; set; }
}