using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestEvents;
using Microsoft.Extensions.Logging;
using Orleans.SyncWork;

namespace Aevatar.Core.Tests.TestGAgents;

[GenerateSerializer]
public class LongRunTaskTestGAgentState : StateBase
{
    [Id(0)] public bool Called { get; set; }
    [Id(1)] public DateTime StartTime { get; set; }
    [Id(2)] public DateTime EndTime { get; set; }
}

[GenerateSerializer]
public class LongRunTaskTestStateLogEvent : StateLogEventBase<LongRunTaskTestStateLogEvent>
{
}

[GAgent]
public class LongRunTaskTestGAgent : GAgentBase<LongRunTaskTestGAgentState, LongRunTaskTestStateLogEvent>
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a GAgent for testing long run tasks");
    }

    [EventHandler]
    public async Task HandleEventAsync(NaiveTestEvent eventData)
    {
        State.StartTime = DateTime.UtcNow;
        await CreateLongRunTaskAsync<NaiveTestEvent, WorkingOnTestEvent>(eventData);
        State.EndTime = DateTime.UtcNow;
    }
    
    [EventHandler]
    public async Task HandleEventAsync(WorkingOnTestEvent eventData)
    {
        State.Called = true;
        Logger.LogInformation("Callback triggered.");
    }
}

public class TestSyncWorker : AevatarSyncWorker<NaiveTestEvent, WorkingOnTestEvent>
{
    public TestSyncWorker(ILogger<AevatarSyncWorker<NaiveTestEvent, WorkingOnTestEvent>> logger,
        LimitedConcurrencyLevelTaskScheduler limitedConcurrencyScheduler) : base(logger, limitedConcurrencyScheduler)
    {
    }

    protected override async Task<WorkingOnTestEvent> PerformLongRunTask(NaiveTestEvent request)
    {
        await Task.Delay(1000);
        return new WorkingOnTestEvent
        {
            Description = "testing long run task."
        };
    }
}