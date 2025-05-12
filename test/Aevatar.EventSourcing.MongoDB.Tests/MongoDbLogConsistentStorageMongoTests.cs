using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aevatar.EventSourcing.Core.Storage;
using Aevatar.EventSourcing.MongoDB.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;
using Moq;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;
using Shouldly;
using Xunit;

namespace Aevatar.EventSourcing.MongoDB.Tests;

[Collection(nameof(MongoDbTestCollection))]
public class MongoDbLogConsistentStorageMongoTests
{
    private readonly Mock<IMongoCollection<BsonDocument>> _mongoCollectionMock;
    private MongoDbLogConsistentStorage _storage;
    private readonly string _name;
    private MongoDbStorageOptions _mongoDbOptions;
    private readonly Mock<IOptions<ClusterOptions>> _clusterOptionsMock;
    private readonly Mock<ILogger<MongoDbLogConsistentStorage>> _loggerMock;
    private readonly Mock<IMongoClient> _mongoClientMock;
    private readonly Mock<IMongoDatabase> _mongoDatabaseMock;
    private readonly Mock<ICluster> _clusterMock;

    public MongoDbLogConsistentStorageMongoTests()
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
        settings.ClusterConfigurator = builder =>
        {
            builder.Subscribe<CommandStartedEvent>(e =>
            {
                if (e.CommandName == "find" || e.CommandName == "insert")
                {
                    throw new MongoException("Test error");
                }
            });
        };

        _mongoDbOptions = new MongoDbStorageOptions
        {
            ClientSettings = settings,
            Database = "TestDb"
        };

        _storage = new MongoDbLogConsistentStorage(_name, _mongoDbOptions, _clusterOptionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ReadAsync_WhenMongoDbError_ThrowsMongoDbStorageException()
    {
        // Arrange
        var grainId = GrainId.Create("TestGrain", "TestKey");
        var grainTypeName = "TestGrainType";

        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Test error"));

        _mongoCollectionMock.Setup(x => x.FindAsync(
            It.IsAny<FilterDefinition<BsonDocument>>(),
            It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Test error"));

        // Initialize the storage
        var observer = new TestSiloLifecycle();
        _storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<MongoDbStorageException>(() =>
            _storage.ReadAsync<TestLogEntry>(grainTypeName, grainId, 0, 1));
    }

    [Fact]
    public async Task GetLastVersionAsync_WhenMongoDbError_ThrowsMongoDbStorageException()
    {
        // Arrange
        var grainId = GrainId.Create("TestGrain", "TestKey");
        var grainTypeName = "TestGrainType";

        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Test error"));

        _mongoCollectionMock.Setup(x => x.FindAsync(
            It.IsAny<FilterDefinition<BsonDocument>>(),
            It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Test error"));

        // Initialize the storage
        var observer = new TestSiloLifecycle();
        _storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<MongoDbStorageException>(() =>
            _storage.GetLastVersionAsync(grainTypeName, grainId));
    }

    [Fact]
    public async Task AppendAsync_WhenMongoDbError_ThrowsMongoDbStorageException()
    {
        // Arrange
        var grainId = GrainId.Create("TestGrain", "TestKey");
        var grainTypeName = "TestGrainType";
        var entries = new List<TestLogEntry> { new TestLogEntry { Data = "Test" } };

        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor.Setup(x => x.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mongoCollectionMock.Setup(x => x.FindAsync(
            It.IsAny<FilterDefinition<BsonDocument>>(),
            It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Test error"));

        _mongoCollectionMock.Setup(x => x.InsertManyAsync(
            It.IsAny<IEnumerable<BsonDocument>>(),
            It.IsAny<InsertManyOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("Test error"));

        // Initialize the storage
        var observer = new TestSiloLifecycle();
        _storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<MongoDbStorageException>(() =>
            _storage.AppendAsync(grainTypeName, grainId, entries, 1));
    }

    [Fact]
    public async Task Init_WhenMongoDbError_ThrowsMongoDbStorageException()
    {
        // Arrange
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ClusterConfigurator = builder =>
        {
            builder.Subscribe<ClusterOpeningEvent>(e =>
            {
                throw new MongoException("Test error");
            });
        };

        var options = new MongoDbStorageOptions
        {
            ClientSettings = settings,
            Database = "TestDb"
        };

        var storage = new MongoDbLogConsistentStorage(_name, options, _clusterOptionsMock.Object, _loggerMock.Object);

        var observer = new TestSiloLifecycle();
        storage.Participate(observer);

        // Act & Assert
        await Assert.ThrowsAsync<MongoDbStorageException>(() => observer.OnStart(CancellationToken.None));
    }

    [Fact]
    public async Task Close_WhenMongoDbError_ThrowsMongoDbStorageException()
    {
        // Arrange
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ClusterConfigurator = builder =>
        {
            builder.Subscribe<ClusterClosingEvent>(e =>
            {
                throw new MongoException("Test error");
            });
        };

        var options = new MongoDbStorageOptions
        {
            ClientSettings = settings,
            Database = "TestDb"
        };

        var storage = new MongoDbLogConsistentStorage(_name, options, _clusterOptionsMock.Object, _loggerMock.Object);

        var observer = new TestSiloLifecycle();
        storage.Participate(observer);
        await observer.OnStart(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<MongoDbStorageException>(() =>
            observer.OnStop(CancellationToken.None));
    }

    [Fact]
    public async Task MongoDbStorageOptionsTests()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ClusterConfigurator = builder =>
        {
            builder.Subscribe<ClusterClosingEvent>(e =>
            {
                throw new MongoException("Test error");
            });
        };

        var options = new MongoDbStorageOptions
        {
            ClientSettings = settings,
            Database = "TestDb",
            InitStage = 0
        };
        options.InitStage.ShouldBe(0);
        options.Credentials.ShouldBeNull();

        var mongoDbOptionValidator = new MongoDbStorageOptionsValidator(options, "test");
        mongoDbOptionValidator.ValidateConfiguration();
        
        mongoDbOptionValidator = new MongoDbStorageOptionsValidator(new MongoDbStorageOptions(), "test");
        Assert.Throws<OrleansConfigurationException>(() => mongoDbOptionValidator.ValidateConfiguration());
    }

    private class TestLogEntry
    {
        public required string Data { get; set; }
    }
} 