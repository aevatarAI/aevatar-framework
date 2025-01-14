using Aevatar.Core.Abstractions;
using Aevatar.ProxyGAgent.Sdk;

namespace Aevatar.Plugins.Test;

[GenerateSerializer]
public class PluginTestEvent1 : EventBase
{
    [Id(0)] public string Greeting { get; set; }
}

[GenerateSerializer]
public class PluginTestEvent2 : EventBase
{
    [Id(0)] public string Greeting { get; set; }
}

public class TestEventHandler : IGAgentEventHandler<PluginTestEvent1>
{
    public async Task<EventHandleResult> HandleEventAsync(PluginTestEvent1 event1Base)
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
            ]
        };
    }
}