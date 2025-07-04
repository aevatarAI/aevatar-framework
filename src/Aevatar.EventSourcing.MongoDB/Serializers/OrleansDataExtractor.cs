using System.Text.Json;
using Aevatar.EventSourcing.Core.Snapshot;

namespace Aevatar.EventSourcing.MongoDB.Serializers;

/// <summary>
/// Extracts state snapshots and event logs from Orleans LogStateWithMetaData format
/// This handles the migration from Orleans complete state storage to separated snapshot/event storage
/// </summary>
public class OrleansDataExtractor
{
    /// <summary>
    /// Extract state snapshot and event history from Orleans LogStateWithMetaData format
    /// </summary>
    public static OrleansDataExtractionResult<TLogView, TLogEntry> ExtractStateAndEvents<TLogView, TLogEntry>(
        string orleansJsonData,
        Func<IEnumerable<TLogEntry>, TLogView> stateRebuilder)
        where TLogView : class, new()
        where TLogEntry : class
    {
        var document = JsonDocument.Parse(orleansJsonData);
        var root = document.RootElement;

        // Extract events from Log.__values array
        var events = new List<TLogEntry>();
        
        if (root.TryGetProperty("Log", out var logElement) &&
            logElement.TryGetProperty("__values", out var valuesElement) &&
            valuesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var eventElement in valuesElement.EnumerateArray())
            {
                try
                {
                    var eventJson = eventElement.GetRawText();
                    var logEntry = JsonSerializer.Deserialize<TLogEntry>(eventJson)!;
                    events.Add(logEntry);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to deserialize event from Orleans LogStateWithMetaData: {ex.Message}", ex);
                }
            }
        }

        // Extract metadata
        var globalVersion = root.TryGetProperty("GlobalVersion", out var versionElement) 
            ? versionElement.GetInt32() 
            : events.Count;
            
        var writeVector = root.TryGetProperty("WriteVector", out var writeVectorElement)
            ? writeVectorElement.GetString() ?? ""
            : "";

        // Rebuild current state by applying all events
        var currentState = stateRebuilder(events);

        // Create snapshot with metadata
        var snapshot = new ViewStateSnapshot<TLogView>(currentState);
        snapshot.State.SnapshotVersion = globalVersion;
        snapshot.State.WriteVector = writeVector;

        return new OrleansDataExtractionResult<TLogView, TLogEntry>
        {
            Snapshot = snapshot,
            Events = events,
            GlobalVersion = globalVersion,
            WriteVector = writeVector
        };
    }

    /// <summary>
    /// Check if the JSON data contains Orleans LogStateWithMetaData format
    /// </summary>
    public static bool IsOrleansLogStateWithMetaData(string jsonData)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonData);
            var root = document.RootElement;
            
            return root.TryGetProperty("__type", out var typeElement) &&
                   typeElement.GetString()?.Contains("LogStateWithMetaData") == true &&
                   root.TryGetProperty("Log", out var logElement) &&
                   logElement.TryGetProperty("__values", out _) &&
                   root.TryGetProperty("GlobalVersion", out _);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Result of extracting data from Orleans LogStateWithMetaData format
/// </summary>
public class OrleansDataExtractionResult<TLogView, TLogEntry>
    where TLogView : class, new()
    where TLogEntry : class
{
    public ViewStateSnapshot<TLogView> Snapshot { get; set; } = null!;
    public List<TLogEntry> Events { get; set; } = new();
    public int GlobalVersion { get; set; }
    public string WriteVector { get; set; } = "";
}