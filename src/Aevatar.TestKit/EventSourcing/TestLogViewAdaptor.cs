using JetBrains.Annotations;
using Orleans;
using Orleans.EventSourcing;
using Orleans.EventSourcing.Common;
using Orleans.Storage;

public class TestLogViewAdaptor<TLogView, TLogEntry> : 
    PrimaryBasedLogViewAdaptor<TLogView, TLogEntry, SubmissionEntry<TLogEntry>>
    where TLogView : class, new()
    where TLogEntry : class
{
    private readonly ILogViewAdaptorHost<TLogView, TLogEntry> _host;
    private readonly TLogView _initialState;
    private readonly IGrainStorage _grainStorage;
    private readonly string _grainTypeName;

    public static readonly ICollection<ViewStateWrapper<TLogView>> SnapshotCollection =
        new List<ViewStateWrapper<TLogView>>();
    public static readonly ICollection<EventLogWrapper<TLogEntry>> EventLogCollection =
        new List<EventLogWrapper<TLogEntry>>();

    private TLogView _confirmedView;
    private int _confirmedVersion;
    private int _globalVersion;

    public TestLogViewAdaptor(ILogViewAdaptorHost<TLogView, TLogEntry> host, TLogView initialState,
        ILogConsistencyProtocolServices services, string grainTypeName, IGrainStorage grainStorage)
        : base(host, initialState, services)
    {
        _host = host;
        _initialState = initialState;
        _grainStorage = grainStorage;
        _grainTypeName = grainTypeName;
    }

    protected override void InitializeConfirmedView(TLogView initialstate)
    {
        _confirmedView = _initialState ?? new TLogView();
        _confirmedVersion = 0;
    }

    protected override TLogView LastConfirmedView()
    {
        return _confirmedView;
    }

    protected override int GetConfirmedVersion() => _confirmedVersion;

    protected override bool SupportSubmissions => true;

    protected override async Task ReadAsync()
    {
        while (true)
        {
            try
            {
                var snapshot = await GetSnapshotAsync();
                if (_confirmedVersion < snapshot?.Version)
                {
                    _confirmedVersion = snapshot.Version;
                    _confirmedView = snapshot.State;
                }
                var eventLogs = await GetAllEventsAsync();
                if (!eventLogs.Any())
                {
                    break;
                }

                var latestVersionOfEventLog = eventLogs.OrderByDescending(e => e.Version).FirstOrDefault();
                if (latestVersionOfEventLog == null)
                {
                    break;
                }

                _globalVersion = latestVersionOfEventLog.Version;
                if (snapshot != null
                    && snapshot.EventLogTimestamp == latestVersionOfEventLog.Timestamp
                    && snapshot.Version == latestVersionOfEventLog.Version)
                {
                    _confirmedVersion = snapshot.Version;
                    _confirmedView = snapshot.State;
                    break;
                }
                
                // TODO: Can only retrieve log segment from _confirmedVersion to _globalVersion
 
                foreach (var eventLog in eventLogs)
                {
                    _host.UpdateView(_confirmedView, eventLog.Event);
                }

                _confirmedVersion += eventLogs.Count;

                LastPrimaryIssue.Resolve(_host, Services);

                break;
            }
            catch (Exception e)
            {
                // unwrap inner exception that was forwarded - helpful for debugging
                if ((e as ProtocolTransportException)?.InnerException != null)
                {
                    e = ((ProtocolTransportException)e).InnerException!;
                }
                LastPrimaryIssue.Record(new ReadFromPrimaryFailed { Exception = e }, Host, Services);
            }
            
            await LastPrimaryIssue.DelayBeforeRetry();
        }
    }

    protected override async Task<int> WriteAsync()
    {
        var snapshot = await GetSnapshotAsync() ?? new ViewStateWrapper<TLogView>
        {
            Version = _confirmedVersion,
            State = _confirmedView
        };

        var latestVersion = snapshot.Version;
        _confirmedVersion = latestVersion;

        var updates = GetCurrentBatchOfUpdates();
        var logsToUpdate = updates
            .Select(e => e.Entry)
            .Select(e => new EventLogWrapper<TLogEntry>
            {
                Version = latestVersion + 1,
                Event = e
            }).ToList();

        // Save logs to database.
        var lastTimestamp = await SaveEventsAsync(logsToUpdate);

        // TODO: Need a easier way.
        _globalVersion = (await GetAllEventsAsync()).Count;

        // Update grain's view.
        foreach (var eventLog in logsToUpdate)
        {
            _host.UpdateView(_confirmedView!, eventLog.Event);
        }

        _confirmedVersion += logsToUpdate.Count;

        // Update the view to database.
        await TakeSnapshotAsync(new ViewStateWrapper<TLogView>
        {
            Version = _confirmedVersion,
            State = _confirmedView,
            EventLogTimestamp = lastTimestamp
        });

        return updates.Length;
    }

    private Task TakeSnapshotAsync(ViewStateWrapper<TLogView> viewState)
    {
        SnapshotCollection.Clear();
        SnapshotCollection.Add(viewState);
        return Task.CompletedTask;
    }

    private Task<DateTime> SaveEventsAsync(IEnumerable<EventLogWrapper<TLogEntry>> eventLogs)
    {
        var timestamp = DateTime.UtcNow;
        foreach (var eventLog in eventLogs)
        {
            eventLog.Timestamp = timestamp;
            EventLogCollection.Add(eventLog);
            timestamp = DateTime.UtcNow;
        }

        return Task.FromResult(timestamp);
    }
    
    [ItemCanBeNull]
    private Task<ViewStateWrapper<TLogView>> GetSnapshotAsync()
    {
        return Task.FromResult(SnapshotCollection
            .ToList()
            .OrderByDescending(v => v.Version)
            .FirstOrDefault());
    }

    private Task<List<EventLogWrapper<TLogEntry>>> GetAllEventsAsync()
    {
        return Task.FromResult(EventLogCollection.ToList());
    }

    protected override SubmissionEntry<TLogEntry> MakeSubmissionEntry(TLogEntry entry)
    {
        return new SubmissionEntry<TLogEntry> { Entry = entry };
    }

    protected override Task<ILogConsistencyProtocolMessage> OnMessageReceived(ILogConsistencyProtocolMessage payload)
    {
        var request = (ReadRequest)payload;

        var response = new ReadResponse<TLogView>() { Version = _confirmedVersion };

        if (_confirmedVersion > request.KnownVersion)
        {
            response.Value = _confirmedView;
        }

        return Task.FromResult<ILogConsistencyProtocolMessage>(response);
    }
}

[Serializable]
[GenerateSerializer]
internal sealed class ReadRequest : ILogConsistencyProtocolMessage
{
    [Id(0)] public int KnownVersion { get; set; }
}

[Serializable]
[GenerateSerializer]
internal sealed class ReadResponse<TViewType> : ILogConsistencyProtocolMessage
{
    [Id(0)] public int Version { get; set; }

    [Id(1)] public TViewType Value { get; set; }
}

public class ViewStateWrapper<T>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int Version { get; set; }
    public T State { get; set; }
    public DateTime EventLogTimestamp { get; set; }
}

public class EventLogWrapper<T>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int Version { get; set; }
    public T Event { get; set; }
    public DateTime Timestamp { get; set; }
}

[Serializable]
[GenerateSerializer]
public sealed class ReadFromPrimaryFailed : PrimaryOperationFailed
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"read from primary failed: caught {Exception.GetType().Name}: {Exception.Message}";
    }
}