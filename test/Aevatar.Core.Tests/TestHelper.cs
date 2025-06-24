using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;

namespace Aevatar.GAgents.Tests;

public static class TestHelper
{
    public static async Task WaitUntilAsync(Func<bool, Task<bool>> predicate, TimeSpan? timeout = null,
        TimeSpan? delayOnFail = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        delayOnFail ??= TimeSpan.FromSeconds(1);
        var keepGoing = new[] { true };

        var task = Loop();
        try
        {
            await Task.WhenAny(task, Task.Delay(timeout.Value));
        }
        finally
        {
            keepGoing[0] = false;
        }

        await task;
        return;

        async Task Loop()
        {
            bool passed;
            do
            {
                // need to wait a bit to before re-checking the condition.
                await Task.Delay(delayOnFail.Value);
                passed = await predicate(false);
            } while (!passed && keepGoing[0]);

            if (!passed)
            {
                await predicate(true);
            }
        }
    }

    public static async Task CheckStateAsync<TState>(IStateGAgent<TState> testGAgent, int expectedCount = 1,
        TimeSpan? timeout = null) where TState : NaiveTestGAgentState, new()
    {
        if (timeout != null)
        {
            timeout = TimeSpan.FromSeconds(20);
        }

        await WaitUntilAsync(_ => PerformCheckStateAsync(testGAgent, expectedCount), timeout);
    }

    private static async Task<bool> PerformCheckStateAsync<TState>(IStateGAgent<TState> testGAgent, int expectedCount)
        where TState : NaiveTestGAgentState, new()
    {
        var state = await testGAgent.GetStateAsync();
        return state.Content.Count == expectedCount;
    }
}