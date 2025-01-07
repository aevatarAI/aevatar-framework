using System.Collections.ObjectModel;
using Orleans.TestKit.Utilities;

namespace Orleans.TestKit;

internal sealed class TestGrainLifecycle : IGrainLifecycle
{
    private readonly Collection<(int Stage, ILifecycleObserver Observer)> _observers = new();

    public void AddMigrationParticipant(IGrainMigrationParticipant participant) { }

    public void RemoveMigrationParticipant(IGrainMigrationParticipant participant) { }

    public IDisposable Subscribe(string observerName, int stage, ILifecycleObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        // TODO: This is a workaround to avoid LogViewAdaptor initialization executed multiple times,
        // should exist a better way to manage grain lifecycle.
        var existingItem = _observers.FirstOrDefault(o => o.Stage == stage);
        if (existingItem.Observer != null)
        {
            _observers.Remove(existingItem);
        }

        var item = (Stage: stage, Observer: observer);
        _observers.Add(item);

        return new LambdaDisposable(() =>
        {
            _observers.Remove(item);
        });
    }

    public Task TriggerStartAsync()
    {
        var tasks = _observers.OrderBy(x => x.Stage).Select(x => x.Observer.OnStart(CancellationToken.None));
        return Task.WhenAll(tasks.ToArray());
    }

    public Task TriggerStopAsync()
    {
        var tasks = _observers.Select(x => x.Observer.OnStop(CancellationToken.None));
        return Task.WhenAll(tasks.ToArray());
    }
}
