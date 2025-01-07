using Aevatar.EventSourcing.Core;
using Aevatar.EventSourcing.Core.Storage;
using Orleans.EventSourcing;
using Orleans.Storage;

namespace Orleans.TestKit;

public class TestLogConsistencyProvider : ILogViewAdaptorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogConsistentStorage _logConsistentStorage;
    private readonly IGrainStorage _grainStorage;

    public TestLogConsistencyProvider(IServiceProvider serviceProvider, ILogConsistentStorage logConsistentStorage, IGrainStorage grainStorage)
    {
        _serviceProvider = serviceProvider;
        _logConsistentStorage = logConsistentStorage;
        _grainStorage = grainStorage;
    }

    public ILogViewAdaptor<TLogView, TLogEntry> MakeLogViewAdaptor<TLogView, TLogEntry>(
        ILogViewAdaptorHost<TLogView, TLogEntry> hostGrain, TLogView initialState,
        string grainTypeName, IGrainStorage grainStorage, ILogConsistencyProtocolServices services)
        where TLogView : class, new() where TLogEntry : class
    {
        return new LogViewAdaptor<TLogView, TLogEntry>(hostGrain, initialState, _grainStorage, grainTypeName,
            new TestLogConsistencyProtocolServices(), new InMemoryLogConsistentStorage(), null);
    }

    public bool UsesStorageProvider => true;
}
