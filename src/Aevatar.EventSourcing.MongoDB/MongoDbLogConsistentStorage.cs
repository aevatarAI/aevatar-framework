using System.Diagnostics;
using System.Text.Json;
using Aevatar.EventSourcing.Core.Storage;
using Aevatar.EventSourcing.MongoDB.Options;
using Aevatar.EventSourcing.MongoDB.Serializers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Orleans.Configuration;
using Orleans.Storage;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;

namespace Aevatar.EventSourcing.MongoDB;

public class MongoDbLogConsistentStorage : ILogConsistentStorage, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly ILogger<MongoDbLogConsistentStorage> _logger;
    private readonly string _name;
    private readonly MongoDbStorageOptions _mongoDbOptions;

    private MongoClient? _client;

    private bool _initialized;
    private readonly string _serviceId;

    private readonly string _fieldData = "snapshot";
    private readonly IGrainStateSerializer _grainStateSerializer;
    public MongoDbLogConsistentStorage(string name, MongoDbStorageOptions options,
        IOptions<ClusterOptions> clusterOptions, ILogger<MongoDbLogConsistentStorage> logger)
    {
        _name = name;
        _mongoDbOptions = options;
        _serviceId = clusterOptions.Value.ServiceId;
        _logger = logger;
        
        if (options.GrainStateSerializer is null)
        {
            throw new ArgumentNullException(nameof(options.GrainStateSerializer), "GrainStateSerializer is required");
        }
        else
        {
            _grainStateSerializer = options.GrainStateSerializer;
        }
    }

    public async Task<IReadOnlyList<TLogEntry>> ReadAsync<TLogEntry>(string grainTypeName, GrainId grainId,
        int fromVersion, int maxCount)
    {
        if (_initialized == false || _client == null || maxCount <= 0)
        {
            return new List<TLogEntry>();
        }

        var collectionName = GetStreamName(grainId);
        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // First check if we have Orleans LogStateWithMetaData format data
            var orleansData = await TryReadOrleansFormatAsync<TLogEntry>(collection, grainId, fromVersion, maxCount);
            if (orleansData != null)
            {
                return orleansData;
            }

            // Regular MongoDB format reading
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("GrainId", grainId.ToString()),
                Builders<BsonDocument>.Filter.Gte("Version", fromVersion)
            );
            var sort = Builders<BsonDocument>.Sort.Ascending("Version");
            var options = new FindOptions<BsonDocument>
            {
                Limit = maxCount,
                Sort = sort
            };

            var documents = await collection.FindAsync(filter, options).ConfigureAwait(false);
            var results = new List<TLogEntry>();

            await documents.ForEachAsync(document =>
            {
                var logEntry = DeserializeLogEntry<TLogEntry>(document);
                results.Add(logEntry);
            }).ConfigureAwait(false);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to read log entries for {GrainType} grain with ID {GrainId} and collection {CollectionName}",
                grainTypeName, grainId, collectionName);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to read log entries for {grainTypeName} with ID {grainId} and collection {collectionName}. {ex.GetType()}: {ex.Message}"));
        }
    }

    private IMongoDatabase GetDatabase()
    {
        return _client!.GetDatabase(_mongoDbOptions.Database);
    }

    /// <summary>
    /// Deserializes log entry with backward compatibility for Memory storage format
    /// </summary>
    private TLogEntry DeserializeLogEntry<TLogEntry>(BsonDocument document)
    {
        // Check if this document has the new MongoDB format (with "snapshot" field)
        if (document.Contains(_fieldData))
        {
            // New MongoDB format: use the grain state serializer
            return _grainStateSerializer.Deserialize<TLogEntry>(document[_fieldData]);
        }
        
        // Check if this document has the Memory format (with "Data" field containing JSON string)
        if (document.Contains("Data"))
        {
            // Memory format: Data field contains JSON string
            var jsonData = document["Data"].AsString;
            return JsonSerializer.Deserialize<TLogEntry>(jsonData)!;
        }
        
        // Fallback: try to deserialize the entire document as the log entry
        try
        {
            // Remove MongoDB specific fields before deserializing
            var logEntryDoc = document.Clone().AsBsonDocument;
            logEntryDoc.Remove("_id");
            logEntryDoc.Remove("GrainId");
            logEntryDoc.Remove("Version");
            
            return BsonSerializer.Deserialize<TLogEntry>(logEntryDoc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize log entry from document: {Document}", document.ToJson());
            throw new MongoDbStorageException($"Unable to deserialize log entry of type {typeof(TLogEntry).Name} from document format.");
        }
    }

    public async Task<int> GetLastVersionAsync(string grainTypeName, GrainId grainId)
    {
        if (_initialized == false || _client == null)
        {
            return -1;
        }

        var collectionName = GetStreamName(grainId);
        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var grainIdString = grainId.ToString();
            var filter = Builders<BsonDocument>.Filter.Eq("GrainId", grainIdString);
            var sort = Builders<BsonDocument>.Sort.Descending("Version");
            var options = new FindOptions<BsonDocument>
            {
                Limit = 1,
                Sort = sort
            };

            var document = await collection.Find(filter).Sort(sort).Limit(1).FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (document == null)
            {
                return -1;
            }

            return document["Version"].AsInt32;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to read last log entry for {GrainType} grain with ID {GrainId} and collection {CollectionName}",
                grainTypeName, grainId, collectionName);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to read last log entry for {grainTypeName} with ID {grainId} and collection {collectionName}. {ex.GetType()}: {ex.Message}"));
        }
    }

    public async Task<int> AppendAsync<TLogEntry>(string grainTypeName, GrainId grainId, IList<TLogEntry> entries,
        int expectedVersion)
    {
        if (_initialized == false || _client == null)
        {
            return -1;
        }

        var collectionName = GetStreamName(grainId);
        if (entries.Count == 0)
        {
            return await GetLastVersionAsync(grainTypeName, grainId);
        }

        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var currentVersion = await GetLastVersionAsync(grainTypeName, grainId).ConfigureAwait(false);
            if (currentVersion != expectedVersion)
            {
                throw new InconsistentStateException(
                    $"Version conflict ({nameof(AppendAsync)}): ServiceId={_serviceId} ProviderName={_name} GrainType={grainTypeName} GrainId={grainId} Version={expectedVersion}.");
            }

            var grainIdString = grainId.ToString();
            var documents = new List<BsonDocument>();

            foreach (var entry in entries)
            {
                currentVersion++;
                
                // Serialize the entry using our grain state serializer
                var data = _grainStateSerializer.Serialize(entry);
                
                var document = new BsonDocument
                {
                    ["GrainId"] = grainIdString,
                    ["Version"] = currentVersion,
                    [_fieldData] = data
                };
                
                documents.Add(document);
            }

            await collection.InsertManyAsync(documents).ConfigureAwait(false);

            return currentVersion;
        }
        catch (Exception ex) when (ex is not InconsistentStateException)
        {
            _logger.LogError(ex,
                "Failed to write log entries for {GrainType} grain with ID {GrainId} and collection {CollectionName}",
                grainTypeName, grainId, collectionName);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to write log entries for {grainTypeName} with ID {grainId} and collection {collectionName}. {ex.GetType()}: {ex.Message}"));
        }
    }

    public void Participate(ISiloLifecycle observer)
    {
        var name = OptionFormattingUtilities.Name<MongoDbLogConsistentStorage>(_name);
        observer.Subscribe(name, _mongoDbOptions.InitStage, Init, Close);
    }

    private async Task Init(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("EventStoreLogConsistentStorage {Name} is initializing: ServiceId={ServiceId}", _name,
                    _serviceId);
            }

            _client = new MongoClient(_mongoDbOptions.ClientSettings);
            
            _initialized = true;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                timer.Stop();
                _logger.LogDebug("Init: Name={Name} ServiceId={ServiceId}, initialized in {ElapsedMilliseconds} ms",
                    _name, _serviceId, timer.Elapsed.TotalMilliseconds.ToString("0.00"));
            }
        }
        catch (Exception ex)
        {
            timer.Stop();
            _logger.LogError(ex, "Init: Name={Name} ServiceId={ServiceId}, errored in {ElapsedMilliseconds} ms", _name,
                _serviceId, timer.Elapsed.TotalMilliseconds.ToString("0.00"));
            throw new MongoDbStorageException(FormattableString.Invariant($"{ex.GetType()}: {ex.Message}"));
        }

        return;
    }
    private async Task Close(CancellationToken cancellationToken)
    {
        if (_initialized == false || _client == null)
        {
            return;
        }

        try
        {
            _client.Cluster.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Close: Name={Name} ServiceId={ServiceId}", _name, _serviceId);
            throw new MongoDbStorageException(FormattableString.Invariant($"{ex.GetType()}: {ex.Message}"));
        }
    }

    private string GetStreamName(GrainId grainId)
    {
        return $"{_serviceId}/{_name}/log/{grainId.Type}";
    }

    /// <summary>
    /// Try to read Orleans LogStateWithMetaData format data and extract events
    /// This handles transparent migration from Orleans native EventSourcing to MongoDB
    /// </summary>
    private async Task<IReadOnlyList<TLogEntry>?> TryReadOrleansFormatAsync<TLogEntry>(
        IMongoCollection<BsonDocument> collection, GrainId grainId, int fromVersion, int maxCount)
    {
        try
        {
            // Look for a single document that might contain Orleans LogStateWithMetaData
            var filter = Builders<BsonDocument>.Filter.Eq("GrainId", grainId.ToString());
            var orleansDocument = await collection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);
            
            if (orleansDocument == null || !orleansDocument.Contains("Data"))
            {
                return null;
            }

            var jsonData = orleansDocument["Data"].AsString;
            if (!OrleansDataExtractor.IsOrleansLogStateWithMetaData(jsonData))
            {
                return null;
            }

            _logger.LogInformation("Detected Orleans LogStateWithMetaData format for {GrainId}, extracting events", grainId);

            // Extract events from Orleans format
            var allEvents = ExtractEventsFromOrleansData<TLogEntry>(jsonData);
            
            // Apply version filtering
            var filteredEvents = allEvents
                .Skip(fromVersion)
                .Take(maxCount)
                .ToList();

            // Background migration: convert to MongoDB format
            _ = Task.Run(async () =>
            {
                try
                {
                    await MigrateOrleansDataToMongoDbFormatAsync(collection, grainId, allEvents, orleansDocument);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background migration failed for {GrainId}", grainId);
                }
            });

            return filteredEvents;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Orleans format data for {GrainId}", grainId);
            return null;
        }
    }

    /// <summary>
    /// Extract events from Orleans LogStateWithMetaData format
    /// </summary>
    private List<TLogEntry> ExtractEventsFromOrleansData<TLogEntry>(string jsonData)
    {
        var events = new List<TLogEntry>();
        
        using var document = JsonDocument.Parse(jsonData);
        var root = document.RootElement;
        
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
                    _logger.LogWarning(ex, "Failed to deserialize event from Orleans LogStateWithMetaData");
                }
            }
        }
        
        return events;
    }

    /// <summary>
    /// Migrate Orleans LogStateWithMetaData to MongoDB separated format
    /// </summary>
    private async Task MigrateOrleansDataToMongoDbFormatAsync<TLogEntry>(
        IMongoCollection<BsonDocument> collection, GrainId grainId, 
        List<TLogEntry> events, BsonDocument originalDocument)
    {
        try
        {
            var grainIdString = grainId.ToString();
            var documents = new List<BsonDocument>();

            // Create individual event documents
            for (int i = 0; i < events.Count; i++)
            {
                var version = i + 1;
                var data = _grainStateSerializer.Serialize(events[i]);
                
                var document = new BsonDocument
                {
                    ["GrainId"] = grainIdString,
                    ["Version"] = version,
                    [_fieldData] = data
                };
                
                documents.Add(document);
            }

            // Replace Orleans document with individual event documents
            using var session = await _client!.StartSessionAsync();
            await session.WithTransactionAsync(async (session, cancellationToken) =>
            {
                // Delete original Orleans document
                await collection.DeleteOneAsync(session, 
                    Builders<BsonDocument>.Filter.Eq("_id", originalDocument["_id"]), 
                    cancellationToken: cancellationToken);
                
                // Insert individual event documents
                if (documents.Count > 0)
                {
                    await collection.InsertManyAsync(session, documents, cancellationToken: cancellationToken);
                }

                return true;
            });

            _logger.LogInformation("Successfully migrated Orleans data to MongoDB format for {GrainId}, {EventCount} events", 
                grainId, events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate Orleans data for {GrainId}", grainId);
            throw;
        }
    }
}