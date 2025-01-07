using System.Collections.Immutable;
using System.Text.Json;
using Aevatar.Core.Abstractions;
using Aevatar.EventSourcing.Core.Exceptions;
using Aevatar.EventSourcing.Core.Snapshot;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.EventSourcing.Common;
using Orleans.Serialization;
using Orleans.Storage;

namespace Aevatar.EventSourcing.Core.Storage;

public partial class LogViewAdaptor<TLogView, TLogEntry>
    : PrimaryBasedLogViewAdaptor<TLogView, TLogEntry, SubmissionEntry<TLogEntry>>
    where TLogView : class, new()
    where TLogEntry : class
{
    private readonly ILogViewAdaptorHost<TLogView, TLogEntry> _host;
    private readonly IGrainStorage? _grainStorage;
    private readonly string _grainTypeName;
    private readonly ILogConsistentStorage _logConsistentStorage;
    private readonly DeepCopier? _deepCopier;

    private TLogView _confirmedView;
    private int _confirmedVersion;
    private int _globalVersion;
    private ViewStateSnapshot<TLogView> _globalSnapshot;

    public LogViewAdaptor(ILogViewAdaptorHost<TLogView, TLogEntry> host, TLogView initialState,
        IGrainStorage? grainStorage, string grainTypeName, ILogConsistencyProtocolServices services,
        ILogConsistentStorage logConsistentStorage, DeepCopier? deepCopier)
        : base(host, initialState, services)
    {
        _host = host;
        _grainStorage = grainStorage;
        _grainTypeName = grainTypeName;
        _logConsistentStorage = logConsistentStorage;
        _deepCopier = deepCopier;
    }

    protected override void InitializeConfirmedView(TLogView initialstate)
    {
        _confirmedView = initialstate;
        _confirmedVersion = 0;
        _globalSnapshot = new ViewStateSnapshot<TLogView>();
        _globalVersion = 0;
    }

    protected override TLogView LastConfirmedView() => _confirmedView;

    protected override int GetConfirmedVersion() => _confirmedVersion;

    protected override bool SupportSubmissions => true;

    public override Task<IReadOnlyList<TLogEntry>> RetrieveLogSegment(int fromVersion, int toVersion)
    {
        var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
        return _logConsistentStorage.ReadAsync<TLogEntry>(_grainTypeName, grainId, fromVersion,
            toVersion - fromVersion + 1);
    }

    protected override SubmissionEntry<TLogEntry> MakeSubmissionEntry(TLogEntry entry)
    {
        return new SubmissionEntry<TLogEntry> { Entry = entry };
    }

    protected override async Task ReadAsync()
    {
        while (true)
        {
            try
            {
                var snapshot = new ViewStateSnapshot<TLogView>();
                await ReadStateAsync(snapshot);
                _globalSnapshot = snapshot;
                Services.Log(LogLevel.Debug, "read success {0}", _globalSnapshot);
                if (_confirmedVersion < _globalSnapshot.State.SnapshotVersion)
                {
                    _confirmedVersion = _globalSnapshot.State.SnapshotVersion;
                    _confirmedView = DeepCopy(_globalSnapshot.State.Snapshot);
                }

                try
                {
                    var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
                    _globalVersion = await _logConsistentStorage.GetLastVersionAsync(_grainTypeName, grainId);
                    if (_confirmedVersion < _globalVersion)
                    {
                        var logEntries = await RetrieveLogSegment(_confirmedVersion, _globalVersion);
                        Services.Log(LogLevel.Debug, "read success {0}", logEntries);
                        UpdateConfirmedView(logEntries);
                    }

                    LastPrimaryIssue.Resolve(Host, Services);
                    break; // successful
                }
                catch (Exception ex)
                {
                    LastPrimaryIssue.Record(new ReadFromLogStorageFailed { Exception = ex }, Host, Services);
                }
            }
            catch (Exception ex)
            {
                LastPrimaryIssue.Record(new ReadFromSnapshotStorageFailed { Exception = ex }, Host, Services);
            }

            Services.Log(LogLevel.Debug, "read failed {0}", LastPrimaryIssue);
            await LastPrimaryIssue.DelayBeforeRetry();
        }
    }

    protected override async Task<int> WriteAsync()
    {
        var updates = GetCurrentBatchOfUpdates();
        var logsSuccessfullyAppended = false;
        var batchSuccessfullyWritten = false;
        var writeBit = _globalSnapshot.State.FlipBit(Services.MyClusterId);
        try
        {
            var logEntries = updates.Select(x => x.Entry).ToImmutableList();
            var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
            _globalVersion =
                await _logConsistentStorage.AppendAsync(_grainTypeName, grainId, logEntries, _globalVersion);
            logsSuccessfullyAppended = true;
            Services.Log(LogLevel.Debug, "write success {0}", logEntries);
            UpdateConfirmedView(logEntries);
        }
        catch (Exception ex)
        {
            LastPrimaryIssue.Record(new UpdateLogStorageFailed { Exception = ex }, Host, Services);
        }

        if (logsSuccessfullyAppended)
        {
            try
            {
                _globalSnapshot.State.Snapshot = DeepCopy(_confirmedView);
                _globalSnapshot.State.SnapshotVersion = _confirmedVersion;
                await WriteStateAsync();
                batchSuccessfullyWritten = true;
                Services.Log(LogLevel.Debug, "write ({0} updates) success {1}", updates.Length, _globalSnapshot);
                LastPrimaryIssue.Resolve(Host, Services);
            }
            catch (Exception ex)
            {
                LastPrimaryIssue.Record(new UpdateSnapshotStorageFailed { Exception = ex }, Host, Services);
            }
        }

        if (!batchSuccessfullyWritten)
        {
            Services.Log(LogLevel.Debug, "write apparently failed {0}", LastPrimaryIssue);
            while (true) // be stubborn until we can read what is there
            {
                await LastPrimaryIssue.DelayBeforeRetry();
                try
                {
                    var snapshot = new ViewStateSnapshot<TLogView>();
                    await ReadStateAsync(snapshot);
                    _globalSnapshot = snapshot;
                    Services.Log(LogLevel.Debug, "read success {0}", _globalSnapshot);
                    if (_confirmedVersion < _globalSnapshot.State.SnapshotVersion)
                    {
                        _confirmedVersion = _globalSnapshot.State.SnapshotVersion;
                        _confirmedView = DeepCopy(_globalSnapshot.State.Snapshot);
                    }

                    try
                    {
                        var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
                        _globalVersion =
                            await _logConsistentStorage.GetLastVersionAsync(_grainTypeName, grainId);
                        if (_confirmedVersion < _globalVersion)
                        {
                            var logEntries = await RetrieveLogSegment(_confirmedVersion, _globalVersion);
                            Services.Log(LogLevel.Debug, "read success {0}", logEntries);
                            UpdateConfirmedView(logEntries);
                        }

                        LastPrimaryIssue.Resolve(Host, Services);
                        break; // successful
                    }
                    catch (Exception ex)
                    {
                        LastPrimaryIssue.Record(new ReadFromLogStorageFailed { Exception = ex }, Host, Services);
                    }
                }
                catch (Exception ex)
                {
                    LastPrimaryIssue.Record(new ReadFromSnapshotStorageFailed { Exception = ex }, Host, Services);
                }

                Services.Log(LogLevel.Debug, "read failed {0}", LastPrimaryIssue);
            }

            // check if last apparently failed write was in fact successful
            if (writeBit == _globalSnapshot.State.GetBit(Services.MyClusterId))
            {
                Services.Log(LogLevel.Debug, "last write ({0} updates) was actually a success {1}", updates.Length,
                    _globalSnapshot);
                batchSuccessfullyWritten = true;
            }
        }

        return batchSuccessfullyWritten ? updates.Length : 0;
    }

    private void UpdateConfirmedView(IReadOnlyList<TLogEntry> logEntries)
    {
        foreach (var logEntry in logEntries)
        {
            try
            {
                _host.UpdateView(_confirmedView, logEntry);
            }
            catch (Exception ex)
            {
                Services.CaughtUserCodeException("UpdateView", nameof(UpdateConfirmedView), ex);
            }
        }

        _confirmedVersion += logEntries.Count;
    }

    protected override Task<ILogConsistencyProtocolMessage> OnMessageReceived(ILogConsistencyProtocolMessage payload)
    {
        var request = (ReadRequest)payload;

        var response = new ReadResponse<TLogView> { Version = _confirmedVersion };

        if (_confirmedVersion > request.KnownVersion)
        {
            response.Value = _confirmedView;
        }

        return Task.FromResult<ILogConsistencyProtocolMessage>(response);
    }

    private TLogView DeepCopy(TLogView state)
    {
        if (_deepCopier == null)
        {
            var json = JsonSerializer.Serialize(state);
            var view = JsonSerializer.Deserialize<TLogView>(json)!;
            return view;
        }

        return _deepCopier.Copy(state);
    }

    private async Task ReadStateAsync(ViewStateSnapshot<TLogView> snapshot)
    {
        if (_grainStorage != null)
        {
            var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
            await _grainStorage.ReadStateAsync(_grainTypeName, grainId, snapshot);
        }
        else
        {
            var grainState = await ((IStateGAgent<TLogView>)_host).GetStateAsync();
            snapshot.State = new ViewStateSnapshotWithMetadata<TLogView>
            {
                Snapshot = grainState
            };
        }
    }

    private async Task WriteStateAsync()
    {
        if (_grainStorage != null)
        {
            var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
            await _grainStorage.WriteStateAsync(_grainTypeName, grainId, _globalSnapshot);
        }
        else
        {
            var entries = await RetrieveLogSegment(0, _confirmedVersion);
            UpdateConfirmedView(entries);
        }
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