using Orleans.Providers.Streams.Common;
using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aevatar.Core.Streaming.Monitors
{
    /// <summary>
    /// Configuration options for stream pressure monitoring.
    /// </summary>
    public class StreamPressureOptions
    {
        /// <summary>
        /// The pressure threshold above which the cache is considered under pressure.
        /// Default is 0.8 (80%).
        /// </summary>
        public double PressureThreshold { get; set; } = 0.8;

        /// <summary>
        /// The interval between pressure checks.
        /// Default is 5 seconds.
        /// </summary>
        public TimeSpan PressureCheckInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The maximum cache size in bytes before pressure is triggered.
        /// Default is 100MB.
        /// </summary>
        public long MaxCacheSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// The maximum number of messages in cache before pressure is triggered.
        /// Default is 10,000 messages.
        /// </summary>
        public long MaxMessageCount { get; set; } = 10_000;
    }

    /// <summary>
    /// Interface for monitoring stream cache pressure.
    /// </summary>
    public interface IStreamPressureMonitor
    {
        /// <summary>
        /// Records cache activity and updates pressure metrics.
        /// </summary>
        /// <param name="messageCount">Number of messages added/removed</param>
        /// <param name="sizeBytes">Size in bytes added/removed</param>
        void RecordCacheActivity(int messageCount, long sizeBytes = 0);

        /// <summary>
        /// Gets the current pressure value (0.0 to 1.0+).
        /// </summary>
        double CurrentPressure { get; }

        /// <summary>
        /// Gets whether the cache is currently under pressure.
        /// </summary>
        bool IsUnderPressure { get; }
    }

    /// <summary>
    /// Monitors stream cache pressure and reports metrics.
    /// </summary>
    public class StreamPressureMonitor : IStreamPressureMonitor
    {
        private readonly ICacheMonitor _cacheMonitor;
        private readonly StreamPressureOptions _options;
        private readonly ILogger<StreamPressureMonitor> _logger;
        
        private long _messageCount;
        private long _cacheSize;
        private DateTime _lastPressureCheck = DateTime.UtcNow;
        private double _currentPressure;
        private bool _isUnderPressure;

        public StreamPressureMonitor(
            ICacheMonitor cacheMonitor,
            IOptions<StreamPressureOptions> options,
            ILogger<StreamPressureMonitor> logger)
        {
            _cacheMonitor = cacheMonitor ?? throw new ArgumentNullException(nameof(cacheMonitor));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public double CurrentPressure => _currentPressure;
        public bool IsUnderPressure => _isUnderPressure;

        public void RecordCacheActivity(int messageCount, long sizeBytes = 0)
        {
            // Update counters atomically
            Interlocked.Add(ref _messageCount, messageCount);
            Interlocked.Add(ref _cacheSize, sizeBytes);

            CheckPressure();
        }

        private void CheckPressure()
        {
            var now = DateTime.UtcNow;
            if (now - _lastPressureCheck < _options.PressureCheckInterval)
                return;

            try
            {
                var currentPressure = CalculatePressure();
                var underPressure = currentPressure > _options.PressureThreshold;

                // Update state
                _currentPressure = currentPressure;
                var wasUnderPressure = _isUnderPressure;
                _isUnderPressure = underPressure;

                // Always report to cache monitor to enable ObservableGauge metrics
                _cacheMonitor.TrackCachePressureMonitorStatusChange(
                    nameof(StreamPressureMonitor),
                    underPressure,
                    _messageCount,
                    currentPressure,
                    _options.PressureThreshold);

                // Log only when status changes to avoid spam
                if (wasUnderPressure != underPressure && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Cache pressure status changed: UnderPressure={UnderPressure}, " +
                        "CurrentPressure={CurrentPressure:F3}, MessageCount={MessageCount}, " +
                        "CacheSize={CacheSize} bytes",
                        underPressure, currentPressure, _messageCount, _cacheSize);
                }

                _lastPressureCheck = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache pressure");
            }
        }

        private double CalculatePressure()
        {
            var currentMessageCount = Interlocked.Read(ref _messageCount);
            var currentCacheSize = Interlocked.Read(ref _cacheSize);

            // Calculate pressure based on both message count and cache size
            var messagePressure = Math.Max(0.0, (double)currentMessageCount / _options.MaxMessageCount);
            var sizePressure = Math.Max(0.0, (double)currentCacheSize / _options.MaxCacheSizeBytes);

            // Use the higher of the two pressures
            return Math.Max(messagePressure, sizePressure);
        }
    }

    /// <summary>
    /// Factory for creating StreamPressureMonitor instances.
    /// </summary>
    public interface IStreamPressureMonitorFactory
    {
        /// <summary>
        /// Creates a pressure monitor for the specified cache monitor.
        /// </summary>
        /// <param name="cacheMonitor">The cache monitor to report to</param>
        /// <returns>A configured pressure monitor instance</returns>
        IStreamPressureMonitor CreatePressureMonitor(ICacheMonitor cacheMonitor);
    }

    /// <summary>
    /// Default implementation of IStreamPressureMonitorFactory.
    /// </summary>
    public class StreamPressureMonitorFactory : IStreamPressureMonitorFactory
    {
        private readonly IOptions<StreamPressureOptions> _options;
        private readonly ILogger<StreamPressureMonitor> _logger;

        public StreamPressureMonitorFactory(
            IOptions<StreamPressureOptions> options,
            ILogger<StreamPressureMonitor> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IStreamPressureMonitor CreatePressureMonitor(ICacheMonitor cacheMonitor)
        {
            return new StreamPressureMonitor(cacheMonitor, _options, _logger);
        }
    }
} 