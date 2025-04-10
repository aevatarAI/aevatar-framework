
using Microsoft.Extensions.Logging;
using Orleans.Providers;

using Aevatar.Core;
using Aevatar.Core.Abstractions;

namespace MessagingGAgent.Grains;

[GenerateSerializer]
public class DemoEvent : EventBase
{
    [Id(0)] public int Number { get; set; } = 0;
}

public class DemoStateLogEvent : StateLogEventBase<DemoStateLogEvent>
{
    public int AddMe { get; set; } = 0;
}


public class DemoGState : StateBase
{
    public int Count { get; set; } = 0;
    
    public void Apply(DemoStateLogEvent @event)
    {
        Count = Count + @event.AddMe;
    }
}
public interface IDemoGAgent : IGAgent
{
    Task<int> GetCount();
}

[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class DemoGAgent : GAgentBase<DemoGState, DemoStateLogEvent>, IDemoGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("A demo counting agent");
    }
    
    [EventHandler]
    public async Task OnAddNumberEvent(DemoEvent @event)
    {
        RaiseEvent(new DemoStateLogEvent() { AddMe = @event.Number });
        Logger.LogInformation($"DemoGAgent received event {@event}");
        await ConfirmEvents();
    }

    public Task<int> GetCount()
    {
        Logger.LogInformation($"GetCount called, current count is {State.Count}");
        return Task.FromResult(State.Count);
    }
}