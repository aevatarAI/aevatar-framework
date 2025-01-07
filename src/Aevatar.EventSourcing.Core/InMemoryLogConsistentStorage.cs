using System.Collections.Concurrent;
using System.Text.Json;
using Aevatar.EventSourcing.Core.Storage;

namespace Aevatar.EventSourcing.Core;

public class LogEntry
{
    public int Version { get; set; }
    public string Data { get; set; } // Adjust the type as needed
}

public class InMemoryLogConsistentStorage : ILogConsistentStorage
{
    private readonly string _name = "EventSourcingTest";
    private readonly string _serviceId = "Aevatar";
    
    /// <summary>
    /// collectionName -> List of log entries
    /// </summary>
    public static readonly ConcurrentDictionary<string, List<LogEntry>> Storage = new();

    public Task<IReadOnlyList<TLogEntry>> ReadAsync<TLogEntry>(string grainTypeName, GrainId grainId,
        int fromVersion, int maxCount)
    {
        if (maxCount <= 0)
        {
            return Task.FromResult<IReadOnlyList<TLogEntry>>(new List<TLogEntry>());
        }

        var collectionName = GetStreamName(grainId);
        if (!Storage.TryGetValue(collectionName, out var entries))
        {
            return Task.FromResult<IReadOnlyList<TLogEntry>>(new List<TLogEntry>());
        }

        var results = entries
            .Where(entry => entry.Version >= fromVersion)
            .OrderBy<LogEntry, object>(entry => entry.Version)
            .Take(maxCount)
            .Select(entry => JsonSerializer.Deserialize<TLogEntry>(entry.Data))
            .ToList();

        return Task.FromResult<IReadOnlyList<TLogEntry>>(results);
    }

    public Task<int> GetLastVersionAsync(string grainTypeName, GrainId grainId)
    {
        var collectionName = GetStreamName(grainId);
        if (!Storage.TryGetValue(collectionName, out var entries) || !entries.Any())
        {
            return Task.FromResult(0);
        }

        var lastVersion = entries.Max(entry => entry.Version);
        return Task.FromResult(lastVersion);
    }

    public async Task<int> AppendAsync<TLogEntry>(string grainTypeName, GrainId grainId, IList<TLogEntry> entries,
        int expectedVersion)
    {
        var collectionName = GetStreamName(grainId);
        if (entries.Count == 0)
        {
            return await GetLastVersionAsync(grainTypeName, grainId);
        }

        if (!Storage.TryGetValue(collectionName, out var logEntries))
        {
            logEntries = new List<LogEntry>();
            Storage[collectionName] = logEntries;
        }

        var currentVersion = await GetLastVersionAsync(grainTypeName, grainId);
        // if (currentVersion != expectedVersion)
        // {
        //     throw new InconsistentStateException(
        //         $"Version conflict ({nameof(AppendAsync)}): ServiceId={_serviceId} ProviderName={_name} GrainType={grainTypeName} GrainId={grainId} Version={expectedVersion}.");
        // }

        foreach (var entry in entries)
        {
            var logEntry = new LogEntry
            {
                Version = ++currentVersion,
                Data = JsonSerializer.Serialize(entry)
            };
            logEntries.Add(logEntry);
        }

        return currentVersion;
    }

    private string GetStreamName(GrainId grainId)
    {
        return $"{_serviceId}/{_name}/log/{grainId}";
    }
}