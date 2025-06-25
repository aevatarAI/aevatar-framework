using Orleans.Providers.Streams.Common;
using Orleans.Runtime;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Aevatar.Core.Streaming.Monitors;

namespace Aevatar.Core.Streaming.Kafka
{
    /// <summary>
    /// A wrapper around IQueueCache that adds monitoring capabilities.
    /// This enables cache metrics collection for stream providers that don't natively support monitoring.
    /// </summary>
    public class MonitoredSimpleQueueCache : IQueueCache, IDisposable
    {
        private readonly IQueueCache _innerCache;
        private readonly ICacheMonitor _cacheMonitor;
        private readonly IStreamPressureMonitor _pressureMonitor;
        private readonly ILogger<MonitoredSimpleQueueCache> _logger;
        private readonly string _queueId;
        
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the MonitoredSimpleQueueCache class.
        /// </summary>
        /// <param name="innerCache">The underlying cache implementation</param>
        /// <param name="cacheMonitor">The cache monitor for metrics collection</param>
        /// <param name="pressureMonitor">The pressure monitor for cache pressure tracking</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="queueId">The queue identifier for logging</param>
        public MonitoredSimpleQueueCache(
            IQueueCache innerCache,
            ICacheMonitor cacheMonitor,
            IStreamPressureMonitor pressureMonitor,
            ILogger<MonitoredSimpleQueueCache> logger,
            string queueId)
        {
            _innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
            _cacheMonitor = cacheMonitor ?? throw new ArgumentNullException(nameof(cacheMonitor));
            _pressureMonitor = pressureMonitor ?? throw new ArgumentNullException(nameof(pressureMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queueId = queueId ?? "unknown";

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created monitored cache for queue {QueueId}", _queueId);
            }
        }

        /// <inheritdoc />
        public void AddToCache(IList<IBatchContainer> messages)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MonitoredSimpleQueueCache));

            if (messages == null || messages.Count == 0)
                return;

            try
            {
                // Add to underlying cache first
                _innerCache.AddToCache(messages);

                // Track metrics
                var messageCount = messages.Count;
                var totalSize = EstimateMessageSize(messages);

                _cacheMonitor.TrackMessagesAdded(messageCount);
                _pressureMonitor.RecordCacheActivity(messageCount, totalSize);

                // Report cache size if we can estimate it
                _cacheMonitor.ReportCacheSize(totalSize);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(
                        "Added {MessageCount} messages to cache {QueueId}, estimated size: {Size} bytes",
                        messageCount, _queueId, totalSize);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding messages to monitored cache {QueueId}", _queueId);
                throw;
            }
        }

        /// <inheritdoc />
        public bool TryPurgeFromCache(out IList<IBatchContainer> purgedItems)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MonitoredSimpleQueueCache));

            try
            {
                var result = _innerCache.TryPurgeFromCache(out purgedItems);
                
                if (result && purgedItems != null && purgedItems.Count > 0)
                {
                    var messageCount = purgedItems.Count;
                    var totalSize = EstimateMessageSize(purgedItems);

                    _cacheMonitor.TrackMessagesPurged(messageCount);
                    _pressureMonitor.RecordCacheActivity(-messageCount, -totalSize);

                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace(
                            "Purged {MessageCount} messages from cache {QueueId}, estimated size: {Size} bytes",
                            messageCount, _queueId, totalSize);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging messages from monitored cache {QueueId}", _queueId);
                throw;
            }
        }

        /// <inheritdoc />
        public IQueueCacheCursor GetCacheCursor(StreamId streamId, StreamSequenceToken token)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MonitoredSimpleQueueCache));

            return _innerCache.GetCacheCursor(streamId, token);
        }

        /// <inheritdoc />
        public bool IsUnderPressure()
        {
            if (_disposed)
                return false;

            // Use our pressure monitor's determination if available, otherwise delegate to inner cache
            if (_pressureMonitor.IsUnderPressure)
                return true;

            return _innerCache.IsUnderPressure();
        }

        /// <inheritdoc />
        public int GetMaxAddCount()
        {
            if (_disposed)
                return 0;

            return _innerCache.GetMaxAddCount();
        }

        /// <summary>
        /// Estimates the size of a collection of batch containers.
        /// This is a rough estimate used for cache size tracking.
        /// </summary>
        /// <param name="messages">The messages to estimate size for</param>
        /// <returns>Estimated size in bytes</returns>
        private static long EstimateMessageSize(IList<IBatchContainer> messages)
        {
            if (messages == null || messages.Count == 0)
                return 0;

            // Rough estimation: each message is approximately 1KB on average
            // This can be made more sophisticated by examining actual message content
            const long averageMessageSize = 1024; // 1KB per message
            return messages.Count * averageMessageSize;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Disposing monitored cache for queue {QueueId}", _queueId);
                }

                // Dispose inner cache if it's disposable
                if (_innerCache is IDisposable disposableCache)
                {
                    disposableCache.Dispose();
                }

                // Dispose pressure monitor if it's disposable
                if (_pressureMonitor is IDisposable disposablePressureMonitor)
                {
                    disposablePressureMonitor.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing monitored cache {QueueId}", _queueId);
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Factory for creating MonitoredSimpleQueueCache instances.
    /// </summary>
    public interface IMonitoredQueueCacheFactory
    {
        /// <summary>
        /// Creates a monitored queue cache that wraps the provided cache with monitoring.
        /// </summary>
        /// <param name="innerCache">The cache to wrap</param>
        /// <param name="queueId">The queue identifier</param>
        /// <param name="providerName">The stream provider name</param>
        /// <returns>A monitored cache instance</returns>
        IQueueCache CreateMonitoredCache(IQueueCache innerCache, string queueId, string providerName);
    }

    /// <summary>
    /// Default implementation of IMonitoredQueueCacheFactory.
    /// </summary>
    public class MonitoredQueueCacheFactory : IMonitoredQueueCacheFactory
    {
        private readonly IAevatarStreamCacheMonitorFactory _cacheMonitorFactory;
        private readonly IStreamPressureMonitorFactory _pressureMonitorFactory;
        private readonly ILogger<MonitoredSimpleQueueCache> _logger;

        public MonitoredQueueCacheFactory(
            IAevatarStreamCacheMonitorFactory cacheMonitorFactory,
            IStreamPressureMonitorFactory pressureMonitorFactory,
            ILogger<MonitoredSimpleQueueCache> logger)
        {
            _cacheMonitorFactory = cacheMonitorFactory ?? throw new ArgumentNullException(nameof(cacheMonitorFactory));
            _pressureMonitorFactory = pressureMonitorFactory ?? throw new ArgumentNullException(nameof(pressureMonitorFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IQueueCache CreateMonitoredCache(IQueueCache innerCache, string queueId, string providerName)
        {
            var cacheMonitor = _cacheMonitorFactory.CreateCacheMonitor(queueId, providerName);
            var pressureMonitor = _pressureMonitorFactory.CreatePressureMonitor(cacheMonitor);

            return new MonitoredSimpleQueueCache(innerCache, cacheMonitor, pressureMonitor, _logger, queueId);
        }
    }
} 