using Aevatar.Core.Abstractions;
using Aevatar.EventSourcing.Core.Exceptions;
using Aevatar.EventSourcing.Core.Storage;
using Aevatar.EventSourcing.MongoDB.Hosting;
using Aevatar.EventSourcing.MongoDB.Options;
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

namespace Aevatar.EventSourcing.MongoDB.Tests;

[Collection("MongoDb")]
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

        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        _mongoDbOptions = new MongoDbStorageOptions
        {
            ClientSettings = settings,
            Database = "TestDb"
        };

        _storage = new MongoDbLogConsistentStorage(_name, _mongoDbOptions, _clusterOptionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Test()
    {
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up any test data using real MongoDB connection
        var client = new MongoClient("mongodb://localhost:27017");
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
        var grainId = GrainId.Create("TestGrain", "TestKey");
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
        var grainId = GrainId.Create("TestGrain", "TestKey");
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
        var grainId = GrainId.Create("TestGrain", "TestKey");
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
        var grainId = GrainId.Create("TestGrain", "TestKey");
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
        var grainId = GrainId.Create("TestGrain", "TestKey");
        var grainTypeName = "TestGrainType";
        var entries = new List<TestLogEntry> { new TestLogEntry { Data = "Test" } };

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
        var grainId = GrainId.Create("TestGrain0", "TestKey");
        var grainTypeName = "TestGrainType";
        var fromVersion = 0;
        var maxCount = 10;

        var testData = new List<BsonDocument>
        {
            new() { { "Data", "Test1" }, { "Version", 0 }, { "GrainId", grainId.ToString() } },
            new() { { "Data", "Test2" }, { "Version", 1 }, { "GrainId", grainId.ToString() } }
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
            .Returns(testData);

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
        Assert.Equal("Test1", result[0].Data);
        Assert.Equal("Test2", result[1].Data);

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

    private class TestLogEntry
    {
        public required string Data { get; set; }
        public  ObjectId _id { get; set; }
        public string GrainId { get; set; }
        public int Version { get; set; }
    }
} 