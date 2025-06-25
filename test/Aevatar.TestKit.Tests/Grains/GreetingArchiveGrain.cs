using TestInterfaces;

namespace TestGrains;

public sealed class GreetingArchiveGrain : Grain<GreetingArchiveGrainState>, IGreetingArchiveGrain
{
    public Task AddGreeting(string greeting)
    {
        State.Greetings.Add(greeting);
        return WriteStateAsync();
    }

    public Task<IEnumerable<string>> GetGreetings() =>
        Task.FromResult<IEnumerable<string>>(State.Greetings);

    public Task ResetGreetings() => ClearStateAsync();
}

public sealed class GreetingArchiveGrainState
{
    public List<string> Greetings { get; private set; } = new();
}
