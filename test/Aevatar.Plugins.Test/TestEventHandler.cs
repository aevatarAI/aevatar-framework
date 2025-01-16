using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.ProxyGAgent;
using Aevatar.ProxyGAgent.Sdk;

namespace Aevatar.Plugins.Test;

[GenerateSerializer]
public class PluginTestEvent : EventBase
{
    [Id(0)] public string Greeting { get; set; }
}

public class TestEventHandler : IGAgentEventHandler<PluginTestEvent>
{
    public async Task<EventHandleResult> HandleEventAsync(PluginTestEvent eventBase)
    {
        return new EventHandleResult
        {
            GAgentEventBase =
            [
                new ProxyGAgentEvent
                {
                    EventData = new Dictionary<string, object>
                    {
                        ["Greeting"] = "Hello from TestEventHandler"
                    }
                }
            ],
            StateLogEventList = 
            [
                new ProxyStateLogEvent
                {
                    Data = new Dictionary<string, object>
                    {
                        ["Test"] = "Raised event from TestEventHandler"
                    }
                }
            ]
        };
    }
}