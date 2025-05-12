using Orleans.Runtime;

namespace Aevatar.EventSourcing.MongoDB.Tests;

public class TestSiloLifecycle : ISiloLifecycle
{
    private readonly Dictionary<string, (int Stage, Func<CancellationToken, Task> OnStart, Func<CancellationToken, Task> OnStop)> _observers = new();
    private int _highestCompletedStage = int.MinValue;
    private int _lowestStoppedStage = int.MaxValue;

    public int HighestCompletedStage => _highestCompletedStage;
    public int LowestStoppedStage => _lowestStoppedStage;

    public IDisposable Subscribe(string observerName, int stage, Func<CancellationToken, Task> onStart, Func<CancellationToken, Task> onStop)
    {
        _observers[observerName] = (stage, onStart, onStop);
        return new TestLifecycleSubscription(() => _observers.Remove(observerName));
    }

    public IDisposable Subscribe(string observerName, int stage, ILifecycleObserver observer)
    {
        return Subscribe(observerName, stage, observer.OnStart, observer.OnStop);
    }

    public async Task OnStart(CancellationToken cancellationToken)
    {
        foreach (var observer in _observers.OrderBy(x => x.Value.Stage))
        {
            await observer.Value.OnStart(cancellationToken);
            _highestCompletedStage = observer.Value.Stage;
        }
    }

    public async Task OnStop(CancellationToken cancellationToken)
    {
        foreach (var observer in _observers.OrderByDescending(x => x.Value.Stage))
        {
            await observer.Value.OnStop(cancellationToken);
            _lowestStoppedStage = observer.Value.Stage;
        }
    }

    private class TestLifecycleSubscription : IDisposable
    {
        private readonly Action _onDispose;

        public TestLifecycleSubscription(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
} 