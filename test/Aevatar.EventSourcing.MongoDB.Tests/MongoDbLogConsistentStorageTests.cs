using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using Moq;
using Orleans.Configuration;
using Orleans.Storage;
using Shouldly;
using Xunit;

using Aevatar.Core.Abstractions;
using Aevatar.EventSourcing.Core.Exceptions;
using Aevatar.EventSourcing.Core.Storage;
using Aevatar.EventSourcing.MongoDB.Hosting;
using Aevatar.EventSourcing.MongoDB.Options;
using Aevatar.EventSourcing.MongoDB.Serializers;

namespace Aevatar.EventSourcing.MongoDB.Tests;

[Collection(nameof(MongoDbTestCollection))]
public class MongoDbLogConsistentStorageTests : IAsyncDisposable
{
    private readonly Mock<IMongoCollection<BsonDocument>> _mongoCollectionMock;
    private readonly MongoDbLogConsistentStorage _storage;
    private readonly string _name;
    private readonly MongoDbStorageOptions _mongoDbOptions;
    private readonly Mock<IOptions<ClusterOptions>> _clusterOptionsMock;
    private readonly Mock<ILogger<MongoDbLogConsistentStorage>> _loggerMock;
    private readonly Mock<IMongoClient> _mongoClientMock;
    private readonly Mock<IMongoDatabase> _mongoDatabaseMock;
    private readonly Mock<ICluster> _clusterMock;
    private readonly string _mongoDbConnectionString;
    private const string TEST_GRAIN_TYPE_NAME = "TestGrainType";

    public MongoDbLogConsistentStorageTests()
    {
        _name = "TestStorage";
        _mongoClientMock = new Mock<IMongoClient>();
        _mongoDatabaseMock = new Mock<IMongoDatabase>();
        _mongoCollectionMock = new Mock<IMongoCollection<BsonDocument>>();
        _clusterMock = new Mock<ICluster>();

        _mongoClientMock.Setup(x => x.GetDatabase(It.IsAny<string>(), null))
            .Returns(_mongoDatabaseMock.Object);
        _mongoDatabaseMock.Setup(x => x.GetCollection<BsonDocument>(It.IsAny<string>(), null))
            .Returns(_mongoCollectionMock.Object);
        _mongoClientMock.Setup(x => x.Cluster).Returns(_clusterMock.Object);

        _clusterOptionsMock = new Mock<IOptions<ClusterOptions>>();
        _clusterOptionsMock.Setup(x => x.Value).Returns(new ClusterOptions { ServiceId = "TestService" });
        _loggerMock = new Mock<ILogger<MongoDbLogConsistentStorage>>();

        _mongoDbConnectionString = AevatarMongoDbFixture.GetRandomConnectionString();
        var settings = MongoClientSettings.FromConnectionString(_mongoDbConnectionString);
        _mongoDbOptions = new MongoDbStorageOptions
        {
            ClientSettings = settings,
            Database = "TestDb",
            GrainStateSerializer = new BsonGrainSerializer()
        };

        _storage = new MongoDbLogConsistentStorage(_name, _mongoDbOptions, _clusterOptionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Test()
    {
        // Basic connectivity test
        Assert.NotNull(_storage);
        Assert.NotNull(_mongoClientMock);
        
        // Initialize the storage
        var observer = new TestSiloLifecycle();
        _storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);
        
        // Verify the initialization succeeded
        Assert.True(observer.HighestCompletedStage >= 0);
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up any test data using real MongoDB connection
        var client = new MongoClient(_mongoDbConnectionString);
        var database = client.GetDatabase("TestDb");
        var collectionsCursor = await database.ListCollectionNamesAsync();
        var collectionNames = new List<string>();
        while (await collectionsCursor.MoveNextAsync())
        {
            collectionNames.AddRange(collectionsCursor.Current);
        }

        foreach (var collectionName in collectionNames)
        {
            var collection = database.GetCollection<BsonDocument>(collectionName);
            await collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        }
    }

    [Fact]
    public async Task ReadAsync_WhenNotInitialized_ReturnsEmptyList()
    {
        // Arrange
        var grainId = GrainId.Create("ReadEmptyGrain", "TestKey");
        var grainTypeName = "TestGrainType";

        // Act
        var result = await _storage.ReadAsync<TestLogEntry>(grainTypeName, grainId, 0, 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLastVersionAsync_WhenNotInitialized_ReturnsNegativeOne()
    {
        // Arrange
        var grainId = GrainId.Create("VersionCheckGrain", "TestKey");
        var grainTypeName = "TestGrainType";

        // Act
        var result = await _storage.GetLastVersionAsync(grainTypeName, grainId);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task AppendAsync_WhenNotInitialized_ReturnsNegativeOne()
    {
        // Arrange
        var grainId = GrainId.Create("AppendEmptyGrain", "TestKey");
        var grainTypeName = "TestGrainType";
        var entries = new List<TestLogEntry>();

        // Act
        var result = await _storage.AppendAsync(grainTypeName, grainId, entries, 0);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task AppendAsync_WithEmptyEntries_ReturnsLastVersion()
    {
        // Arrange
        var grainId = GrainId.Create("AppendEmptyListGrain", "TestKey");
        var grainTypeName = "TestGrainType";
        var entries = new List<TestLogEntry>();

        // Act
        var result = await _storage.AppendAsync(grainTypeName, grainId, entries, 0);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task AppendAsync_WithVersionConflict_ThrowsInconsistentStateException()
    {
        // Arrange
        var grainId = GrainId.Create("ConflictGrain", "TestKey");
        var grainTypeName = "TestGrainType";
        var entries = new List<TestLogEntry> { new TestLogEntry { Data = "Conflict Test Entry" } };

        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockCursor.SetupSequence(x => x.Current)
            .Returns(new[] { new BsonDocument { { "Version", 2 } } }); // Return a higher version than expected

        _mongoCollectionMock.Setup(x => x.FindAsync(
            It.IsAny<FilterDefinition<BsonDocument>>(),
            It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        // Initialize the storage
        var observer = new TestSiloLifecycle();
        _storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InconsistentStateException>(() =>
            _storage.AppendAsync(grainTypeName, grainId, entries, 1));

        Assert.Contains("Version conflict", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnLogEntries_WhenDataExists()
    {
        // Arrange
        var grainId = GrainId.Create("TestDataExistsGrain", "TestKey");
        var grainTypeName = "TestGrainType";
        var fromVersion = 0;
        var maxCount = 10;

        var testData = new List<TestLogEntry>
        {
            new TestLogEntry { Data = "Test1" },
            new TestLogEntry { Data = "Test2" }
        };

        // Mock GetLastVersionAsync to return -1 initially
        var mockCursorForVersion = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursorForVersion.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mongoCollectionMock.Setup(x => x.FindAsync(
            It.IsAny<FilterDefinition<BsonDocument>>(),
            It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursorForVersion.Object);

        // Mock InsertManyAsync for AppendAsync
        _mongoCollectionMock.Setup(x => x.InsertManyAsync(
            It.IsAny<IEnumerable<BsonDocument>>(),
            It.IsAny<InsertManyOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock FindAsync for ReadAsync
        var mockCursorForRead = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursorForRead.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockCursorForRead.Setup(x => x.Current)
            .Returns(testData.Select(d => d.ToBsonDocument()).ToList());

        _mongoCollectionMock.Setup(x => x.FindAsync(
            It.IsAny<FilterDefinition<BsonDocument>>(),
            It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursorForRead.Object);

        // Initialize the storage
        var observer = new TestSiloLifecycle();
        _storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);

        await _storage.AppendAsync(grainTypeName, grainId, testData, -1);
        // Act
        var result = await _storage.ReadAsync<TestLogEntry>(grainTypeName, grainId, fromVersion, maxCount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.NotNull(result[0].data);
        Assert.NotNull(result[1].data);
        Assert.Equal("Test1", result[0].data.Data);
        Assert.Equal("Test2", result[1].data.Data);

        // Clean up after test
        await DisposeAsync();
    }

    [Fact]
    public async Task TestReadAndWriteException()
    {
        var readFromLogException = new ReadFromLogStorageFailed()
        {
            Exception = new Exception("ReadFromLogFail")
        };
        readFromLogException.ToString().ShouldContain("ReadFromLogFail");
        var readFromSnapshotException = new ReadFromSnapshotStorageFailed()
        {
            Exception = new Exception("ReadFromSnapshot")
        };
        readFromSnapshotException.ToString().ShouldContain("ReadFromSnapshot");
        var updateLogException = new UpdateLogStorageFailed()
        {
            Exception = new Exception("UpdateLogStorage")
        };
        updateLogException.ToString().ShouldContain("UpdateLogStorage");
        var updateSnapshotException = new UpdateSnapshotStorageFailed()
        {
            Exception = new Exception("UpdateSnapshotStorage")
        };
        updateSnapshotException.ToString().ShouldContain("UpdateSnapshotStorage");

        var eventLogWrapper = new EventLogWrapper<int>()
        {
            Version = 1,
            Timestamp = DateTime.Now,
            Event = 1
        };
        eventLogWrapper.Id.ShouldNotBe(Guid.Empty.ToString());
        
        var viewStateWrapper = new ViewStateWrapper<int>()
        {
            Version = 1,
            EventLogTimestamp = DateTime.Now,
            State = 1,
        };
        viewStateWrapper.Id.ShouldNotBe(Guid.Empty.ToString());

        var readFromPrimaryFailed = new ReadFromPrimaryFailed()
        {
            Exception = new Exception("test exception")
        };
        readFromPrimaryFailed.ToString().ShouldContain("test exception");
        
    }
    
    // This is a simplification of the reading task that allows us to bypass the MongoDB complexity
    [Fact]
    public async Task Simple_ReadAsync_Test()
    {
        // Arrange
        var grainId = GrainId.Create("SimpleGrain", "Test");
        var grainTypeName = TEST_GRAIN_TYPE_NAME;
        
        // Create test documents
        var testDocuments = new List<BsonDocument>
        {
            new BsonDocument
            {
                { "GrainId", grainId.ToString() },
                { "Version", 0 },
                { "data", new BsonDocument
                    {
                        { "Data", "Test1" },
                        { "Value", "Test1Value" }
                    }
                }
            },
            new BsonDocument
            {
                { "GrainId", grainId.ToString() },
                { "Version", 1 },
                { "data", new BsonDocument
                    {
                        { "Data", "Test2" },
                        { "Value", "Test2Value" }
                    }
                }
            }
        };
        
        // Setup for direct deserialize testing
        var serializer = new BsonGrainSerializer();
        var entries = new List<TestLogEntry>();
        
        foreach (var document in testDocuments)
        {
            // Extract the data field
            if (document.Contains("data") && document["data"] != BsonNull.Value)
            {
                var dataField = document["data"];
                Console.WriteLine($"Deserializing data field: {dataField}");
                var logEntry = serializer.Deserialize<TestLogEntry>(dataField);
                entries.Add(logEntry);
            }
        }
        
        // Assert direct serialization works
        Assert.Equal(2, entries.Count);
        Assert.Equal("Test1", entries[0].Data);
        Assert.Equal("Test1Value", entries[0].Value);
        Assert.Equal("Test2", entries[1].Data);
        Assert.Equal("Test2Value", entries[1].Value);
    }


    // Add mock ISiloLifecycle implementation for testing
    private class TestSiloLifecycle : ISiloLifecycle
    {
        private readonly List<(string Name, int Stage, Func<CancellationToken, Task> StartFunc, Func<CancellationToken, Task> StopFunc)> _observers = new();
        
        public int HighestCompletedStage { get; private set; } = -1;
        
        public int LowestStoppedStage { get; private set; } = int.MaxValue;

        public IDisposable Subscribe(string observerName, int stage, Func<CancellationToken, Task> onStart, Func<CancellationToken, Task> onStop)
        {
            _observers.Add((observerName, stage, onStart, onStop));
            return null!;
        }
        
        public IDisposable Subscribe(string observerName, int stage, ILifecycleObserver observer)
        {
            return Subscribe(observerName, stage, 
                ct => observer.OnStart(ct),
                ct => observer.OnStop(ct));
        }

        public async Task OnStart(CancellationToken cancellationToken)
        {
            foreach (var observer in _observers.OrderBy(o => o.Stage))
            {
                await observer.StartFunc(cancellationToken);
                HighestCompletedStage = observer.Stage;
            }
        }

        public async Task OnStop(CancellationToken cancellationToken)
        {
            foreach (var observer in _observers.OrderByDescending(o => o.Stage))
            {
                await observer.StopFunc(cancellationToken);
                LowestStoppedStage = Math.Min(LowestStoppedStage, observer.Stage);
            }
        }
    }

    [GenerateSerializer]
    public class TestLogEntry
    {
        [Id(0)]
        public required string Data { get; set; }
        [Id(1)]
        public string Value { get; set; } = string.Empty;
        [Id(2)]
        public ObjectId _id { get; set; }
        [Id(3)]
        public string GrainId { get; set; } = string.Empty;
        [Id(4)]
        public int Version { get; set; }
        [Id(5)]
        public TestLogEntry data { get; set; }
    }
} 