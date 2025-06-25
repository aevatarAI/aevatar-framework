using Orleans.Configuration;
using Orleans.Providers.Streams.Common;
using Orleans.Serialization;
using Orleans.Streams;
using Orleans.Streams.Kafka.Config;
using Orleans.Streams.Kafka.Core;
using Orleans.Streams.Utils.Serialization;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aevatar.Core.Streaming.Monitors;

namespace Aevatar.Core.Streaming.Kafka
{
    /// <summary>
    /// Aevatar-specific Kafka adapter factory that adds monitoring capabilities to the standard Kafka stream provider.
    /// This factory wraps the Kafka queue adapter cache with monitoring to collect the missing Orleans streaming metrics.
    /// </summary>
    public class AevatarKafkaAdapterFactory : IQueueAdapterFactory
    {
        private readonly KafkaAdapterFactory _innerFactory;
        private readonly IMonitoredQueueCacheFactory _monitoredCacheFactory;
        private readonly ILogger<AevatarKafkaAdapterFactory> _logger;
        private readonly string _providerName;

        /// <summary>
        /// Initializes a new instance of the AevatarKafkaAdapterFactory class.
        /// </summary>
        /// <param name="innerFactory">The underlying Kafka adapter factory</param>
        /// <param name="monitoredCacheFactory">Factory for creating monitored caches</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="providerName">The stream provider name</param>
        public AevatarKafkaAdapterFactory(
            KafkaAdapterFactory innerFactory,
            IMonitoredQueueCacheFactory monitoredCacheFactory,
            ILogger<AevatarKafkaAdapterFactory> logger,
            string providerName)
        {
            _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
            _monitoredCacheFactory = monitoredCacheFactory ?? throw new ArgumentNullException(nameof(monitoredCacheFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created AevatarKafkaAdapterFactory for provider {ProviderName}", _providerName);
            }
        }

        /// <inheritdoc />
        public Task<IQueueAdapter> CreateAdapter()
        {
            return _innerFactory.CreateAdapter();
        }

        /// <inheritdoc />
        public IQueueAdapterCache GetQueueAdapterCache()
        {
            var originalCache = _innerFactory.GetQueueAdapterCache();
            return new MonitoredQueueAdapterCache(originalCache, _monitoredCacheFactory, _providerName, _logger);
        }

        /// <inheritdoc />
        public IStreamQueueMapper GetStreamQueueMapper()
        {
            return _innerFactory.GetStreamQueueMapper();
        }

        /// <inheritdoc />
        public Task<IStreamFailureHandler> GetDeliveryFailureHandler(QueueId queueId)
        {
            return _innerFactory.GetDeliveryFailureHandler(queueId);
        }

        /// <summary>
        /// Creates an AevatarKafkaAdapterFactory from service provider configuration.
        /// </summary>
        /// <param name="services">The service provider</param>
        /// <param name="name">The provider name</param>
        /// <returns>A configured AevatarKafkaAdapterFactory instance</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or name is null</exception>
        public static AevatarKafkaAdapterFactory Create(IServiceProvider services, string name)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            try
            {
                // Create the underlying Kafka adapter factory
                var innerFactory = KafkaAdapterFactory.Create(services, name);
                
                // Get required services for monitoring
                var monitoredCacheFactory = services.GetRequiredService<IMonitoredQueueCacheFactory>();
                var logger = services.GetRequiredService<ILogger<AevatarKafkaAdapterFactory>>();

                return new AevatarKafkaAdapterFactory(innerFactory, monitoredCacheFactory, logger, name);
            }
            catch (Exception ex)
            {
                var logger = services.GetService<ILogger<AevatarKafkaAdapterFactory>>();
                logger?.LogError(ex, "Failed to create AevatarKafkaAdapterFactory for provider {ProviderName}", name);
                throw;
            }
        }
    }

    /// <summary>
    /// Monitored queue adapter cache that wraps the original cache with monitoring capabilities.
    /// </summary>
    public class MonitoredQueueAdapterCache : IQueueAdapterCache
    {
        private readonly IQueueAdapterCache _innerCache;
        private readonly IMonitoredQueueCacheFactory _monitoredCacheFactory;
        private readonly string _providerName;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the MonitoredQueueAdapterCache class.
        /// </summary>
        /// <param name="innerCache">The underlying queue adapter cache</param>
        /// <param name="monitoredCacheFactory">Factory for creating monitored caches</param>
        /// <param name="providerName">The stream provider name</param>
        /// <param name="logger">Logger instance</param>
        public MonitoredQueueAdapterCache(
            IQueueAdapterCache innerCache,
            IMonitoredQueueCacheFactory monitoredCacheFactory,
            string providerName,
            ILogger logger)
        {
            _innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
            _monitoredCacheFactory = monitoredCacheFactory ?? throw new ArgumentNullException(nameof(monitoredCacheFactory));
            _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IQueueCache CreateQueueCache(QueueId queueId)
        {
            try
            {
                // Create the original cache
                var originalCache = _innerCache.CreateQueueCache(queueId);
                
                // Wrap it with monitoring
                var monitoredCache = _monitoredCacheFactory.CreateMonitoredCache(
                    originalCache, 
                    queueId.ToString(), 
                    _providerName);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Created monitored queue cache for queue {QueueId} in provider {ProviderName}",
                        queueId, _providerName);
                }

                return monitoredCache;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to create monitored queue cache for queue {QueueId} in provider {ProviderName}",
                    queueId, _providerName);
                throw;
            }
        }
    }
} 