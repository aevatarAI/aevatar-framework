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
                var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
                Services.Log(LogLevel.Information, "Starting ReadAsync for grain ");
                
                // 1. First read framework format snapshot
                var snapshot = new ViewStateSnapshot<TLogView>();
                try
                {
                    await ReadStateAsync(snapshot);
                    _globalSnapshot = snapshot;
                    Services.Log(LogLevel.Information, "Successfully read snapshot: RecordExists={0}, SnapshotVersion={1}", 
                        _globalSnapshot.RecordExists, _globalSnapshot.State.SnapshotVersion);
                }
                catch (Exception readEx)
                {
                    Services.Log(LogLevel.Warning, "Failed to read snapshot: {0}, attempting Orleans format conversion", readEx.Message);
                    
                    // If reading snapshot fails, try Orleans format conversion directly
                    _globalSnapshot = new ViewStateSnapshot<TLogView>();
                    await TryConvertOrleansLogStorageAsync(grainId);
                    
                    LastPrimaryIssue.Resolve(Host, Services);
                    break; // successful
                }
                
                // 2. If snapshot data exists, process normally
                if (_globalSnapshot.State.SnapshotVersion > 0)
                {
                    Services.Log(LogLevel.Information, "Found framework snapshot, processing normally");
                    await ProcessFrameworkDataAsync(grainId);
                }
                else
                {
                    // 3. Check if Orleans memory event structure needs conversion
                    Services.Log(LogLevel.Information, "No framework snapshot found, attempting Orleans format conversion");
                    await TryConvertOrleansLogStorageAsync(grainId);
                }
                
                LastPrimaryIssue.Resolve(Host, Services);
                break; // successful
            }
            catch (Exception ex)
            {
                LastPrimaryIssue.Record(new ReadFromSnapshotStorageFailed { Exception = ex }, Host, Services);
                Services.Log(LogLevel.Debug, "read failed {0}", LastPrimaryIssue);
                await LastPrimaryIssue.DelayBeforeRetry();
            }
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
            
            Services.Log(LogLevel.Information, "Enhanced Orleans LogViewAdaptor.ReadStateAsync called for grain ");
            
            // Enhanced Orleans compatibility: Try framework format first, fall back to Orleans format
            try
            {
                Services.Log(LogLevel.Debug, "Attempting to read framework format snapshot for grain {0}", grainId);
                await _grainStorage.ReadStateAsync(_grainTypeName, grainId, snapshot);
                Services.Log(LogLevel.Debug, "Successfully read framework format snapshot: RecordExists={0}", snapshot.RecordExists);
            }
            catch (Exception ex) when (IsOrleansFormatException(ex))
            {
                Services.Log(LogLevel.Information, "Enhanced Orleans Framework format read failed with FormatException, attempting Orleans format conversion: {0}", ex.Message);
                
                // Try Orleans format reading when framework format fails
                await ReadOrleansFormatAndConvertAsync(grainId, snapshot, ex);
            }
            catch (Exception ex)
            {
                Services.Log(LogLevel.Error, "Enhanced Orleans Unexpected error in ReadStateAsync: {0}", ex.Message);
                throw;
            }
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

    /// <summary>
    /// Check if the exception indicates Orleans format incompatibility
    /// </summary>
    private static bool IsOrleansFormatException(Exception ex)
    {
        return ex is FormatException formatEx && 
               (formatEx.Message.Contains("Input string was not in a correct format") ||
                formatEx.Message.Contains("Expected an ASCII digit") ||
                formatEx.Message.Contains("Failure to parse"));
    }

    /// <summary>
    /// Read Orleans format data and convert to framework format
    /// </summary>
    private async Task ReadOrleansFormatAndConvertAsync(GrainId grainId, ViewStateSnapshot<TLogView> snapshot, Exception originalException)
    {
        try
        {
            // Use Orleans LogStateWithMetaDataAndETag to read Orleans format directly
            var orleansLogState = new Orleans.EventSourcing.LogStorage.LogStateWithMetaDataAndETag<TLogEntry>();
            Services.Log(LogLevel.Information, "Reading Orleans LogStateWithMetaDataAndETag format for grain ");
            
            await _grainStorage.ReadStateAsync(_grainTypeName, grainId, orleansLogState);
            
            Services.Log(LogLevel.Information, "Orleans format read result: RecordExists={0}, LogCount={1}, GlobalVersion={2}", 
                orleansLogState.RecordExists, 
                orleansLogState.State?.Log?.Count ?? 0,
                orleansLogState.State?.GlobalVersion ?? 0);
            
            if (orleansLogState.RecordExists && orleansLogState.State?.Log != null)
            {
                Services.Log(LogLevel.Information, "Successfully read Orleans format data, converting to framework format");
                
                // Convert Orleans data to framework snapshot format
                var convertedSnapshot = await ConvertOrleansToFrameworkSnapshot(orleansLogState);
                
                // Copy converted data to output snapshot
                snapshot.RecordExists = true;
                snapshot.State = convertedSnapshot.State;
                snapshot.ETag = convertedSnapshot.ETag;
                
                Services.Log(LogLevel.Information, "Orleans→Framework snapshot conversion completed: Version={0}, WriteVector='{1}'", 
                    snapshot.State?.SnapshotVersion ?? 0, snapshot.State?.WriteVector ?? "empty");
            }
            else
            {
                Services.Log(LogLevel.Information, "No Orleans data found, initializing empty framework snapshot");
                
                // Initialize empty framework snapshot
                snapshot.RecordExists = false;
                snapshot.State = new ViewStateSnapshotWithMetadata<TLogView>
                {
                    Snapshot = new TLogView(),
                    SnapshotVersion = 0,
                    WriteVector = string.Empty
                };
            }
        }
        catch (Exception orleansEx)
        {
            Services.Log(LogLevel.Warning, "Orleans format reading also failed: {0}, falling back to empty snapshot", orleansEx.Message);
            Services.Log(LogLevel.Debug, "Original framework error: {0}", originalException.Message);
            
            // Initialize empty snapshot when both formats fail
            snapshot.RecordExists = false;
            snapshot.State = new ViewStateSnapshotWithMetadata<TLogView>
            {
                Snapshot = new TLogView(),
                SnapshotVersion = 0,
                WriteVector = string.Empty
            };
        }
    }

    /// <summary>
    /// Convert Orleans LogStateWithMetaDataAndETag to Framework ViewStateSnapshot
    /// </summary>
    private async Task<ViewStateSnapshot<TLogView>> ConvertOrleansToFrameworkSnapshot(Orleans.EventSourcing.LogStorage.LogStateWithMetaDataAndETag<TLogEntry> orleansLogState)
    {
        var frameworkSnapshot = new ViewStateSnapshot<TLogView>();
        
        // Replay Orleans events to build current state
        var currentView = new TLogView();
        var version = 0;
        
        if (orleansLogState.State?.Log != null)
        {
            foreach (var logEntry in orleansLogState.State.Log)
            {
                try
                {
                    _host.UpdateView(currentView, logEntry);
                    version++;
                    Services.Log(LogLevel.Debug, "Applied Orleans event {0}: {1}", version, logEntry.GetType().Name);
                }
                catch (Exception ex)
                {
                    Services.CaughtUserCodeException("UpdateView", nameof(ConvertOrleansToFrameworkSnapshot), ex);
                    Services.Log(LogLevel.Warning, "Failed to apply Orleans event {0}: {1}", version + 1, ex.Message);
                }
            }
            
            // OPTIMIZED: Set initial version to preserve version continuity without heavy write operations
            if (orleansLogState.State.Log.Count > 0)
            {
                try
                {
                    var grainId = Services.GrainId.IsDefault ? ((IGrain)_host).GetGrainId() : Services.GrainId;
                    Services.Log(LogLevel.Information, "Setting initial version {0} for version continuity (Orleans had {1} events)", 
                        version, orleansLogState.State.Log.Count);
                    
                    // Set initial version to preserve version continuity
                    await _logConsistentStorage.SetInitialVersionAsync(_grainTypeName, grainId, version-1);
                    
                    // Update global version with Orleans version
                    _globalVersion = version;
                    _confirmedVersion = version;
                    
                    Services.Log(LogLevel.Information, "Successfully set initial version {0} for version continuity", version);
                }
                catch (Exception ex)
                {
                    Services.Log(LogLevel.Error, "Failed to set initial version in MongoDB: {0}", ex.Message);
                    Services.Log(LogLevel.Warning, "Continuing with snapshot-only conversion, version continuity may be lost");
                }
            }
        }
        
        // Convert Orleans WriteVector to framework format
        var orleansWriteVector = orleansLogState.State?.WriteVector ?? string.Empty;
        var frameworkWriteVector = ConvertOrleansWriteVectorToFrameworkFormat(orleansWriteVector);
        
        // Create framework snapshot
        frameworkSnapshot.RecordExists = true;
        frameworkSnapshot.ETag = orleansLogState.ETag ?? string.Empty;
        frameworkSnapshot.State = new ViewStateSnapshotWithMetadata<TLogView>
        {
            Snapshot = DeepCopy(currentView),
            SnapshotVersion = version,
            WriteVector = frameworkWriteVector
        };
        
        // Update internal state
        _confirmedView = DeepCopy(currentView);
        _confirmedVersion = version;
        _globalVersion = Math.Max(version, orleansLogState.State?.GlobalVersion ?? 0);
        
        Services.Log(LogLevel.Information, "Orleans→Framework conversion: {0} events → Version {1}, GlobalVersion {2}", 
            orleansLogState.State?.Log?.Count ?? 0, version, _globalVersion);
        
        return frameworkSnapshot;
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

    /// <summary>
    /// Standard logic for processing framework format data
    /// </summary>
    private async Task ProcessFrameworkDataAsync(GrainId grainId)
    {
        if (_confirmedVersion < _globalSnapshot.State.SnapshotVersion)
        {
            _confirmedVersion = _globalSnapshot.State.SnapshotVersion;
            _confirmedView = DeepCopy(_globalSnapshot.State.Snapshot);
        }

        try
        {
            _globalVersion = await _logConsistentStorage.GetLastVersionAsync(_grainTypeName, grainId);
            if (_confirmedVersion < _globalVersion)
            {
                var logEntries = await RetrieveLogSegment(_confirmedVersion, _globalVersion);
                Services.Log(LogLevel.Debug, "read framework events success {0}", logEntries);
                UpdateConfirmedView(logEntries);
            }
        }
        catch (Exception ex)
        {
            LastPrimaryIssue.Record(new ReadFromLogStorageFailed { Exception = ex }, Host, Services);
            throw;
        }
    }

    /// <summary>
    /// Enhanced Orleans compatibility: Convert Orleans Memory EventSourcing format to Framework MongoDB format
    /// This method is called when reading framework format fails, to provide seamless Orleans→Framework compatibility
    /// </summary>
    private async Task TryConvertOrleansLogStorageAsync(GrainId grainId)
    {
        Services.Log(LogLevel.Information, "Enhanced Orleans compatibility: Attempting Orleans to Framework conversion for grain {0}", grainId);
        
        if (_grainStorage == null) 
        {
            Services.Log(LogLevel.Information, "No grain storage available for Orleans conversion, using initial state");
            return;
        }
        
        try
        {
            // Use Orleans LogStateWithMetaDataAndETag class directly to read Orleans format
            var orleansLogState = new Orleans.EventSourcing.LogStorage.LogStateWithMetaDataAndETag<TLogEntry>();
            Services.Log(LogLevel.Information, "Reading Orleans LogStorage format for grain ");
            
            await _grainStorage.ReadStateAsync(_grainTypeName, grainId, orleansLogState);
            
            Services.Log(LogLevel.Information, "Orleans LogStorage read result: RecordExists={0}, LogCount={1}, GlobalVersion={2}", 
                orleansLogState.RecordExists, 
                orleansLogState.State?.Log?.Count ?? 0,
                orleansLogState.State?.GlobalVersion ?? 0);
            
            if (orleansLogState.RecordExists && orleansLogState.State?.Log != null)
            {
                var eventCount = orleansLogState.State.Log.Count;
                var globalVersion = orleansLogState.State.GlobalVersion;
                
                Services.Log(LogLevel.Information, "Found Orleans data: {0} events, GlobalVersion={1}, converting to Framework format", 
                    eventCount, globalVersion);
                
                // Use the enhanced ConvertOrleansToFrameworkSnapshot method
                var frameworkSnapshot = await ConvertOrleansToFrameworkSnapshot(orleansLogState);
                
                // Update global snapshot with converted data
                _globalSnapshot = frameworkSnapshot;
                
                Services.Log(LogLevel.Information, "Orleans→Framework conversion completed successfully: {0} events replayed, version {1}, WriteVector '{2}'", 
                    eventCount, _confirmedVersion, _globalSnapshot.State.WriteVector);
                
                // Save the converted data in framework format for future use
                try
                {
                    await WriteStateAsync();
                    Services.Log(LogLevel.Information, "Converted Orleans data saved in Framework format for future access");
                }
                catch (Exception ex)
                {
                    Services.Log(LogLevel.Warning, "Failed to save converted Orleans data: {0}", ex.Message);
                }
            }
            else
            {
                Services.Log(LogLevel.Information, "No Orleans events found (RecordExists={0}), initializing with empty state", 
                    orleansLogState.RecordExists);
                
                // Initialize empty state if no Orleans data
                _confirmedView = new TLogView();
                _confirmedVersion = 0;
                _globalVersion = 0;
                _globalSnapshot.State.Snapshot = DeepCopy(_confirmedView);
                _globalSnapshot.State.SnapshotVersion = 0;
                _globalSnapshot.State.WriteVector = string.Empty;
            }
        }
        catch (FormatException ex) when (ex.Message.Contains("Input string was not in a correct format"))
        {
            // This is the specific error we're trying to fix
            Services.Log(LogLevel.Error, "Orleans WriteVector FormatException detected: {0}", ex.Message);
            Services.Log(LogLevel.Information, "Attempting to preserve existing MongoDB version numbers");
            
            // Try to preserve existing MongoDB version numbers instead of resetting to 0
            try
            {
                var actualVersion = await _logConsistentStorage.GetLastVersionAsync(_grainTypeName, grainId);
                Services.Log(LogLevel.Information, "Found existing MongoDB version: {0}", actualVersion);
                
                _confirmedVersion = Math.Max(0, actualVersion);
                _globalVersion = Math.Max(0, actualVersion);
                _globalSnapshot.State.SnapshotVersion = _confirmedVersion;
                
                // Initialize state with preserved version
                _confirmedView = new TLogView();
                _globalSnapshot.State.Snapshot = DeepCopy(_confirmedView);
                _globalSnapshot.State.WriteVector = string.Empty;
            }
            catch (Exception versionEx)
            {
                Services.Log(LogLevel.Warning, "Failed to get existing MongoDB version: {0}", versionEx.Message);
                
                // Fallback to reset version if MongoDB version retrieval fails
                _confirmedView = new TLogView();
                _confirmedVersion = 0;
                _globalVersion = 0;
                _globalSnapshot.State.Snapshot = DeepCopy(_confirmedView);
                _globalSnapshot.State.SnapshotVersion = 0;
                _globalSnapshot.State.WriteVector = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Services.Log(LogLevel.Warning, "Orleans LogStorage reading failed: {0}", ex.Message);
            Services.Log(LogLevel.Debug, "Orleans compatibility exception details: {0}", ex.ToString());
            Services.Log(LogLevel.Information, "Attempting to preserve existing MongoDB version numbers");
            
            // Try to preserve existing MongoDB version numbers instead of resetting to 0
            try
            {
                var actualVersion = await _logConsistentStorage.GetLastVersionAsync(_grainTypeName, grainId);
                Services.Log(LogLevel.Information, "Found existing MongoDB version: {0}", actualVersion);
                
                _confirmedVersion = Math.Max(0, actualVersion);
                _globalVersion = Math.Max(0, actualVersion);
                _globalSnapshot.State.SnapshotVersion = _confirmedVersion;
                
                // Initialize state with preserved version
                _confirmedView = new TLogView();
                _globalSnapshot.State.Snapshot = DeepCopy(_confirmedView);
                _globalSnapshot.State.WriteVector = string.Empty;
            }
            catch (Exception versionEx)
            {
                Services.Log(LogLevel.Warning, "Failed to get existing MongoDB version: {0}", versionEx.Message);
                
                // Fallback to reset version if MongoDB version retrieval fails
                _confirmedView = new TLogView();
                _confirmedVersion = 0;
                _globalVersion = 0;
                _globalSnapshot.State.Snapshot = DeepCopy(_confirmedView);
                _globalSnapshot.State.SnapshotVersion = 0;
                _globalSnapshot.State.WriteVector = string.Empty;
            }
        }
    }

    /// <summary>
    /// Convert Orleans WriteVector format to Framework compatible format
    /// Orleans format: ",replica1,replica2" -> Framework format: "replica1;replica2"
    /// </summary>
    private string ConvertOrleansWriteVectorToFrameworkFormat(string orleansWriteVector)
    {
        if (string.IsNullOrEmpty(orleansWriteVector))
            return string.Empty;
            
        try
        {
            // Orleans WriteVector typically starts with comma: ",replica1,replica2"
            if (orleansWriteVector.StartsWith(","))
            {
                var converted = orleansWriteVector.TrimStart(',').Replace(",", ";");
                Services.Log(LogLevel.Debug, "Converted Orleans WriteVector '{0}' to Framework format '{1}'", 
                    orleansWriteVector, converted);
                return converted;
            }
            
            // If it doesn't start with comma, check if it needs conversion
            if (orleansWriteVector.Contains(","))
            {
                var converted = orleansWriteVector.Replace(",", ";");
                Services.Log(LogLevel.Debug, "Converted Orleans WriteVector '{0}' to Framework format '{1}'", 
                    orleansWriteVector, converted);
                return converted;
            }
            
            // Already in compatible format
            Services.Log(LogLevel.Debug, "Orleans WriteVector '{0}' is already in compatible format", orleansWriteVector);
            return orleansWriteVector;
        }
        catch (Exception ex)
        {
            Services.Log(LogLevel.Warning, "Failed to convert Orleans WriteVector '{0}': {1}, using empty", 
                orleansWriteVector, ex.Message);
            return string.Empty;
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