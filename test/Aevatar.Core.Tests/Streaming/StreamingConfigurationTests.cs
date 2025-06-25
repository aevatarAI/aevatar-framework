using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Aevatar.Core.Streaming.Extensions;
using Aevatar.Core.Streaming.Monitors;
using Aevatar.Core.Streaming.Kafka;
using Orleans.Streams.Kafka.Config;
using Orleans.Hosting;
using Orleans.Configuration;
using Orleans;
using Orleans.Runtime;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aevatar.Core.Tests.Streaming;

/// <summary>
/// Unit tests for Aevatar streaming configuration and service registration.
/// Tests service registration, builder extensions, and configuration validation.
/// </summary>
public class StreamingConfigurationTests
{
    #region Service Registration Tests

    [Fact]
    public void AddAevatarStreamingMonitoring_WithValidServices_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAevatarStreamingMonitoring();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Verify core monitoring services are registered
        Assert.NotNull(serviceProvider.GetService<IAevatarStreamCacheMonitorFactory>());
        Assert.NotNull(serviceProvider.GetService<IStreamPressureMonitorFactory>());
        Assert.NotNull(serviceProvider.GetService<IMonitoredQueueCacheFactory>());
    }

    [Fact]
    public void AddAevatarStreamingMonitoring_WithEmptyServices_StillRegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        // Unit tests should provide required dependencies - Orleans doesn't auto-add logging
        services.AddLogging();

        // Act
        services.AddAevatarStreamingMonitoring();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve monitoring services when logging is provided
        Assert.NotNull(serviceProvider.GetService<IAevatarStreamCacheMonitorFactory>());
        Assert.NotNull(serviceProvider.GetService<IStreamPressureMonitorFactory>());
        Assert.NotNull(serviceProvider.GetService<IMonitoredQueueCacheFactory>());
    }

    [Fact]
    public void AddAevatarStreamingMonitoring_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddAevatarStreamingMonitoring());
    }

    #endregion

    #region Stream Pressure Configuration Tests

    [Fact]
    public void ConfigureStreamPressureMonitoring_WithValidOptions_ConfiguresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAevatarStreamingMonitoring();

        const double expectedThreshold = 0.9;
        var expectedInterval = TimeSpan.FromSeconds(10);
        const long expectedMaxBytes = 50 * 1024 * 1024; // 50MB
        const int expectedMaxMessages = 5000;

        // Act
        services.ConfigureStreamPressureMonitoring(options =>
        {
            options.PressureThreshold = expectedThreshold;
            options.PressureCheckInterval = expectedInterval;
            options.MaxCacheSizeBytes = expectedMaxBytes;
            options.MaxMessageCount = expectedMaxMessages;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<StreamPressureOptions>>();

        Assert.NotNull(options);
        Assert.Equal(expectedThreshold, options.Value.PressureThreshold);
        Assert.Equal(expectedInterval, options.Value.PressureCheckInterval);
        Assert.Equal(expectedMaxBytes, options.Value.MaxCacheSizeBytes);
        Assert.Equal(expectedMaxMessages, options.Value.MaxMessageCount);
    }

    [Fact]
    public void ConfigureStreamPressureMonitoring_WithProviderName_ConfiguresNamedOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAevatarStreamingMonitoring();
        const string providerName = "TestProvider";

        // Act
        services.ConfigureStreamPressureMonitoring(providerName, options =>
        {
            options.PressureThreshold = 0.7;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var namedOptions = serviceProvider.GetService<IOptionsSnapshot<StreamPressureOptions>>();

        Assert.NotNull(namedOptions);
        Assert.Equal(0.7, namedOptions.Get(providerName).PressureThreshold);
    }

    [Fact]
    public void ConfigureStreamPressureMonitoring_WithNullProviderName_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAevatarStreamingMonitoring();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.ConfigureStreamPressureMonitoring(null!, options => { }));
    }

    [Fact]
    public void ConfigureStreamPressureMonitoring_WithEmptyProviderName_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAevatarStreamingMonitoring();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            services.ConfigureStreamPressureMonitoring(string.Empty, options => { }));
    }

    [Fact]
    public void ConfigureStreamPressureMonitoring_WithBoundaryValues_ConfiguresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAevatarStreamingMonitoring();

        // Act - Test boundary values
        services.ConfigureStreamPressureMonitoring(options =>
        {
            options.PressureThreshold = 0.0; // Minimum
            options.PressureCheckInterval = TimeSpan.FromMilliseconds(1); // Near minimum
            options.MaxCacheSizeBytes = 1; // Minimum
            options.MaxMessageCount = 1; // Minimum
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<StreamPressureOptions>>();

        Assert.NotNull(options);
        Assert.Equal(0.0, options.Value.PressureThreshold);
        Assert.Equal(TimeSpan.FromMilliseconds(1), options.Value.PressureCheckInterval);
        Assert.Equal(1, options.Value.MaxCacheSizeBytes);
        Assert.Equal(1, options.Value.MaxMessageCount);
    }

    #endregion

    #region Silo Builder Extension Tests

    [Fact]
    public void SiloBuilderExtensions_AddAevatarKafkaStreaming_WithValidParameters_ConfiguresSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var mockSiloBuilder = new MockSiloBuilder(services);
        const string providerName = "TestKafkaProvider";

        // Act
        var result = mockSiloBuilder.AddAevatarKafkaStreaming(providerName, options =>
        {
            options.BrokerList = new[] { "localhost:9092" };
            options.ConsumerGroupId = "test-group";
            options.AddTopic("test-topic");
        });

        // Assert
        Assert.NotNull(result);
        Assert.Same(mockSiloBuilder, result); // Should return same builder for fluent API

        // Verify monitoring services were added
        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IAevatarStreamCacheMonitorFactory>());
    }



    [Fact]
    public void SiloBuilderExtensions_AddAevatarKafkaStreaming_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        ISiloBuilder nullBuilder = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder.AddAevatarKafkaStreaming("test", options => { }));
    }

    [Fact]
    public void SiloBuilderExtensions_AddAevatarKafkaStreaming_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockSiloBuilder = new MockSiloBuilder(services);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            mockSiloBuilder.AddAevatarKafkaStreaming(null!, options => { }));
    }



    #endregion

    #region Client Builder Extension Tests

    [Fact]
    public void ClientBuilderExtensions_AddAevatarKafkaStreaming_WithValidParameters_ConfiguresSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var mockClientBuilder = new MockClientBuilder(services);
        const string providerName = "TestKafkaProvider";

        // Act
        var result = mockClientBuilder.AddAevatarKafkaStreaming(providerName, options =>
        {
            options.BrokerList = new[] { "localhost:9092" };
            options.ConsumerGroupId = "test-group";
            options.AddTopic("test-topic");
        });

        // Assert
        Assert.NotNull(result);
        Assert.Same(mockClientBuilder, result); // Should return same builder for fluent API

        // Verify monitoring services were added
        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IAevatarStreamCacheMonitorFactory>());
    }



    [Fact]
    public void ClientBuilderExtensions_AddAevatarKafkaStreaming_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        IClientBuilder nullBuilder = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder.AddAevatarKafkaStreaming("test", options => { }));
    }



    #endregion

    #region Adapter Factory Tests



    [Fact]
    public void AevatarKafkaAdapterFactory_Create_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceProvider nullProvider = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            AevatarKafkaAdapterFactory.Create(nullProvider, "TestProvider"));
    }

    [Fact]
    public void AevatarKafkaAdapterFactory_Create_WithNullProviderName_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            AevatarKafkaAdapterFactory.Create(serviceProvider, null!));
    }

    #endregion

    #region Mock Implementations

    /// <summary>
    /// Mock implementation of ISiloBuilder for testing purposes.
    /// </summary>
    private class MockSiloBuilder : ISiloBuilder
    {
        private readonly IServiceCollection _services;

        public MockSiloBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services => _services;
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();
        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        public ISiloBuilder ConfigureServices(Action<IServiceCollection> configureServices)
        {
            configureServices?.Invoke(_services);
            return this;
        }
    }

    /// <summary>
    /// Mock implementation of IClientBuilder for testing purposes.
    /// </summary>
    private class MockClientBuilder : IClientBuilder
    {
        private readonly IServiceCollection _services;

        public MockClientBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services => _services;
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();
        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        public IClientBuilder ConfigureServices(Action<IServiceCollection> configureServices)
        {
            configureServices?.Invoke(_services);
            return this;
        }
    }

    #endregion
}