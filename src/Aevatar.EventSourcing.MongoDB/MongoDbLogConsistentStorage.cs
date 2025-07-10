using System.Diagnostics;
using Aevatar.EventSourcing.Core.Storage;
using Aevatar.EventSourcing.MongoDB.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Aevatar.EventSourcing.MongoDB.Serializers;
using MongoDB.Driver;
using Orleans.Configuration;
using Orleans.Storage;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using MongoDB.Bson.IO;

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
                try
                {
                    // 🔄 Enhanced Orleans compatibility - Check for Orleans format data
                    var logEntry = DeserializeLogEntryWithOrleansCompatibility<TLogEntry>(document, grainId, fromVersion);
                    results.Add(logEntry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🚨 Failed to deserialize log entry for {GrainType} grain {GrainId} - Document: {Document}", 
                        grainTypeName, grainId, document.ToJson());
                    throw;
                }
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

    public async Task SetInitialVersionAsync(string grainTypeName, GrainId grainId, int initialVersion)
    {
        var grainIdString = grainId.ToString();
        var collectionName = GetStreamName(grainId);
        
        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);
            
            // Check if any data already exists
            var existingVersion = await GetLastVersionAsync(grainTypeName, grainId);
            if (existingVersion >= 0)
            {
                _logger.LogInformation("Grain {GrainId} already has version {ExistingVersion}, skipping initial version setup", 
                    grainId, existingVersion);
                return;
            }
            
            // Create a placeholder document with the initial version
            var placeholderDocument = new BsonDocument
            {
                ["GrainId"] = grainIdString,
                ["Version"] = initialVersion,
                [_fieldData] = BsonDocument.Parse("{\"_t\":\"MigrationPlaceholder\",\"Message\":\"Version placeholder for Orleans migration\"}")
            };
            
            await collection.InsertOneAsync(placeholderDocument).ConfigureAwait(false);
            
            _logger.LogInformation("Set initial version {InitialVersion} for grain {GrainId} in collection {CollectionName}", 
                initialVersion, grainId, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to set initial version {InitialVersion} for {GrainType} grain with ID {GrainId} and collection {CollectionName}",
                initialVersion, grainTypeName, grainId, collectionName);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to set initial version {initialVersion} for {grainTypeName} with ID {grainId} and collection {collectionName}. {ex.GetType()}: {ex.Message}"));
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
    /// 🔄 Enhanced Orleans compatibility - Deserialize log entry with Orleans format detection
    /// </summary>
    private TLogEntry DeserializeLogEntryWithOrleansCompatibility<TLogEntry>(BsonDocument document, GrainId grainId, int fromVersion)
    {
        // First, try to deserialize using Aevatar framework format
        try
        {
            if (document.Contains(_fieldData))
            {
                _logger.LogDebug("🔄 Attempting Aevatar framework deserialization for grain {GrainId}", grainId);
                var logEntry = _grainStateSerializer.Deserialize<TLogEntry>(document[_fieldData]);
                _logger.LogDebug("✅ Successfully deserialized using Aevatar framework format for grain {GrainId}", grainId);
                return logEntry;
            }
        }
        catch (Exception ex) when (IsOrleansFormatException(ex))
        {
            _logger.LogWarning("🔄 Framework deserialization failed with Orleans format pattern - attempting Orleans compatibility mode for grain {GrainId}. Error: {Error}", 
                grainId, ex.Message);
        }

        // Attempt Orleans format deserialization
        try
        {
            _logger.LogInformation("🔄 Attempting Orleans format deserialization for grain {GrainId}", grainId);
            
            // Check for Orleans format: document should have _doc.data structure
            if (document.Contains("_doc"))
            {
                var docField = document["_doc"];
                if (docField is BsonDocument docDocument && docDocument.Contains("data"))
                {
                    _logger.LogInformation("🔍 Orleans format detected: _doc.data structure found for grain {GrainId}", grainId);
                    
                    var dataField = docDocument["data"];
                    if (dataField is BsonBinaryData binaryData)
                    {
                        _logger.LogInformation("📦 Orleans binary data found - size: {Size} bytes for grain {GrainId}", 
                            binaryData.Bytes.Length, grainId);
                        
                        // Try to deserialize the binary data using Orleans serialization
                        var logEntry = DeserializeOrleansData<TLogEntry>(binaryData.Bytes, grainId);
                        
                        _logger.LogInformation("✅ Orleans→Framework Successfully converted Orleans format data for grain {GrainId}", grainId);
                        return logEntry;
                    }
                }
            }
            
            // Alternative: Check for direct binary data field (Orleans might store data differently)
            if (document.Contains("data") && document["data"] is BsonBinaryData directBinaryData)
            {
                _logger.LogInformation("🔍 Orleans format detected: direct data field found for grain {GrainId}", grainId);
                var logEntry = DeserializeOrleansData<TLogEntry>(directBinaryData.Bytes, grainId);
                _logger.LogInformation("✅ Orleans→Framework Successfully converted direct Orleans format data for grain {GrainId}", grainId);
                return logEntry;
            }

            _logger.LogError("❌ No recognizable data format found in document for grain {GrainId}. Document structure: {Document}", 
                grainId, document.ToJson());
            throw new MongoDbStorageException($"No recognizable data format found for grain {grainId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Orleans format deserialization failed for grain {GrainId}", grainId);
            throw new MongoDbStorageException($"Both Aevatar and Orleans format deserialization failed for grain {grainId}: {ex.Message}");
        }
    }

    /// <summary>
    /// 🔍 Check if exception indicates Orleans format incompatibility
    /// </summary>
    private static bool IsOrleansFormatException(Exception ex)
    {
        // Check for the specific FormatException pattern we've observed
        return ex is FormatException formatEx &&
               (formatEx.Message.Contains("Input string was not in a correct format") ||
                formatEx.Message.Contains("Failure to parse near offset") ||
                formatEx.Message.Contains("Expected an ASCII digit"));
    }

    /// <summary>
    /// 🔄 Deserialize Orleans binary data to framework format
    /// </summary>
    private TLogEntry DeserializeOrleansData<TLogEntry>(byte[] binaryData, GrainId grainId)
    {
        try
        {
            _logger.LogDebug("🔄 Attempting Orleans binary deserialization for grain {GrainId} - data size: {Size}", 
                grainId, binaryData.Length);

            // For now, attempt to use BsonSerializer to deserialize the binary data
            // This may need to be enhanced based on actual Orleans serialization format
            using var memoryStream = new MemoryStream(binaryData);
            using var bsonReader = new BsonBinaryReader(memoryStream);
            
            var document = BsonSerializer.Deserialize<BsonDocument>(bsonReader);
            _logger.LogDebug("📖 Successfully parsed Orleans binary data to BSON for grain {GrainId}", grainId);
            
            // Try to deserialize as our expected format
            var logEntry = _grainStateSerializer.Deserialize<TLogEntry>(document);
            _logger.LogDebug("✅ Successfully deserialized Orleans binary data for grain {GrainId}", grainId);
            
            return logEntry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to deserialize Orleans binary data for grain {GrainId}", grainId);
            
            // Alternative approach: try direct JSON deserialization if BSON fails
            try
            {
                var jsonString = System.Text.Encoding.UTF8.GetString(binaryData);
                _logger.LogDebug("🔄 Attempting Orleans JSON deserialization for grain {GrainId}: {Json}", 
                    grainId, jsonString.Length > 200 ? jsonString.Substring(0, 200) + "..." : jsonString);
                
                var document = BsonDocument.Parse(jsonString);
                var logEntry = _grainStateSerializer.Deserialize<TLogEntry>(document);
                
                _logger.LogInformation("✅ Successfully deserialized Orleans JSON data for grain {GrainId}", grainId);
                return logEntry;
            }
            catch (Exception jsonEx)
            {
                _logger.LogError(jsonEx, "❌ Both BSON and JSON Orleans deserialization failed for grain {GrainId}", grainId);
                throw new MongoDbStorageException($"Orleans data deserialization failed for grain {grainId}: BSON error: {ex.Message}, JSON error: {jsonEx.Message}");
            }
        }
    }
}