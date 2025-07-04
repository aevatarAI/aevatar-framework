using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aevatar.EventSourcing.MongoDB.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Orleans.Configuration;
using Orleans.Storage;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;

namespace Aevatar.EventSourcing.MongoDB;

/// <summary>
/// MongoDB implementation of IGrainStorage for storing snapshots
/// This works alongside MongoDbLogConsistentStorage to provide complete EventSourcing storage
/// </summary>
public class MongoDbGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly ILogger<MongoDbGrainStorage> _logger;
    private readonly string _name;
    private readonly MongoDbStorageOptions _mongoDbOptions;
    private readonly IGrainStateSerializer _grainStateSerializer;
    private readonly string _serviceId;

    private MongoClient? _client;
    private bool _initialized;

    public MongoDbGrainStorage(
        string name, 
        MongoDbStorageOptions options,
        IOptions<ClusterOptions> clusterOptions, 
        ILogger<MongoDbGrainStorage> logger)
    {
        _name = name;
        _mongoDbOptions = options;
        _serviceId = clusterOptions.Value.ServiceId;
        _logger = logger;
        
        if (options.GrainStateSerializer is null)
        {
            throw new ArgumentNullException(nameof(options.GrainStateSerializer), "GrainStateSerializer is required");
        }
        
        _grainStateSerializer = options.GrainStateSerializer;
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        if (_initialized == false || _client == null)
        {
            return;
        }

        var collectionName = GetSnapshotCollectionName(grainId, stateName);
        
        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var filter = Builders<BsonDocument>.Filter.Eq("GrainId", grainId.ToString());
            var document = await collection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);

            if (document != null)
            {
                grainState.ETag = document.GetValue("ETag", BsonNull.Value).AsString;
                grainState.RecordExists = true;
                
                if (document.Contains("snapshot"))
                {
                    grainState.State = _grainStateSerializer.Deserialize<T>(document["snapshot"]);
                }
            }
            else
            {
                grainState.ETag = null;
                grainState.RecordExists = false;
                grainState.State = Activator.CreateInstance<T>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read snapshot for {GrainType} grain with ID {GrainId}", 
                typeof(T).Name, grainId);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to read snapshot for {typeof(T).Name} with ID {grainId}. {ex.GetType()}: {ex.Message}"));
        }
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        if (_initialized == false || _client == null)
        {
            return;
        }

        var collectionName = GetSnapshotCollectionName(grainId, stateName);
        
        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var newETag = Guid.NewGuid().ToString();
            var serializedState = _grainStateSerializer.Serialize(grainState.State);

            var document = new BsonDocument
            {
                ["GrainId"] = grainId.ToString(),
                ["snapshot"] = serializedState,
                ["ETag"] = newETag,
                ["LastUpdated"] = DateTime.UtcNow
            };

            if (grainState.RecordExists)
            {
                // Update existing document
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("GrainId", grainId.ToString()),
                    Builders<BsonDocument>.Filter.Eq("ETag", grainState.ETag)
                );

                var result = await collection.ReplaceOneAsync(filter, document).ConfigureAwait(false);
                
                if (result.MatchedCount == 0)
                {
                    throw new InconsistentStateException(
                        $"ETag mismatch for {typeof(T).Name} with ID {grainId}. Expected ETag: {grainState.ETag}");
                }
            }
            else
            {
                // Insert new document
                await collection.InsertOneAsync(document).ConfigureAwait(false);
                grainState.RecordExists = true;
            }

            grainState.ETag = newETag;
        }
        catch (Exception ex) when (ex is not InconsistentStateException)
        {
            _logger.LogError(ex, "Failed to write snapshot for {GrainType} grain with ID {GrainId}", 
                typeof(T).Name, grainId);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to write snapshot for {typeof(T).Name} with ID {grainId}. {ex.GetType()}: {ex.Message}"));
        }
    }

    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        if (_initialized == false || _client == null)
        {
            return;
        }

        var collectionName = GetSnapshotCollectionName(grainId, stateName);
        
        try
        {
            var database = GetDatabase();
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var filter = Builders<BsonDocument>.Filter.Eq("GrainId", grainId.ToString());
            await collection.DeleteOneAsync(filter).ConfigureAwait(false);

            grainState.ETag = null;
            grainState.RecordExists = false;
            grainState.State = Activator.CreateInstance<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear snapshot for {GrainType} grain with ID {GrainId}", 
                typeof(T).Name, grainId);
            throw new MongoDbStorageException(FormattableString.Invariant(
                $"Failed to clear snapshot for {typeof(T).Name} with ID {grainId}. {ex.GetType()}: {ex.Message}"));
        }
    }

    public void Participate(ISiloLifecycle observer)
    {
        var name = OptionFormattingUtilities.Name<MongoDbGrainStorage>(_name);
        observer.Subscribe(name, _mongoDbOptions.InitStage, Init, Close);
    }

    private Task Init(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("MongoDbGrainStorage {Name} is initializing: ServiceId={ServiceId}", 
                    _name, _serviceId);
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
            _logger.LogError(ex, "Init: Name={Name} ServiceId={ServiceId}, errored in {ElapsedMilliseconds} ms", 
                _name, _serviceId, timer.Elapsed.TotalMilliseconds.ToString("0.00"));
            throw new MongoDbStorageException(FormattableString.Invariant($"{ex.GetType()}: {ex.Message}"));
        }

        return Task.CompletedTask;
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

    private IMongoDatabase GetDatabase()
    {
        return _client!.GetDatabase(_mongoDbOptions.Database);
    }

    private string GetSnapshotCollectionName(GrainId grainId, string stateName)
    {
        return $"{_serviceId}/{_name}/snapshot/{grainId.Type}";
    }
}

/// <summary>
/// Factory for creating MongoDbGrainStorage instances
/// </summary>
public static class MongoDbGrainStorageFactory
{
    public static MongoDbGrainStorage Create(IServiceProvider serviceProvider, object name)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<MongoDbStorageOptions>>().Get((string)name);
        var clusterOptions = serviceProvider.GetRequiredService<IOptions<ClusterOptions>>();
        var logger = serviceProvider.GetRequiredService<ILogger<MongoDbGrainStorage>>();
        
        return new MongoDbGrainStorage((string)name, options, clusterOptions, logger);
    }
}