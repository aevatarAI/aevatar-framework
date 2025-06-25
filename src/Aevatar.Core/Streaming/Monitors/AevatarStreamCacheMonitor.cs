using Orleans.Providers.Streams.Common;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Orleans.Configuration;

namespace Aevatar.Core.Streaming.Monitors
{
    /// <summary>
    /// Aevatar-specific cache monitor that extends DefaultCacheMonitor with proper labeling
    /// for cluster_id, silo_id, queue_id as required by the metrics design document.
    /// </summary>
    public class AevatarStreamCacheMonitor : DefaultCacheMonitor
    {
        /// <summary>
        /// Initializes a new instance of the AevatarStreamCacheMonitor class.
        /// </summary>
        /// <param name="clusterId">The cluster identifier from ClusterOptions</param>
        /// <param name="siloId">The silo identifier from SiloOptions</param>
        /// <param name="queueId">The queue identifier</param>
        /// <param name="providerName">The stream provider name</param>
        public AevatarStreamCacheMonitor(
            string clusterId,
            string siloId,
            string queueId,
            string providerName)
            : base(CreateDimensions(clusterId, siloId, queueId, providerName))
        {
        }

        /// <summary>
        /// Initializes a new instance of the AevatarStreamCacheMonitor class using options.
        /// </summary>
        /// <param name="clusterOptions">Cluster configuration options</param>
        /// <param name="siloOptions">Silo configuration options</param>
        /// <param name="queueId">The queue identifier</param>
        /// <param name="providerName">The stream provider name</param>
        public AevatarStreamCacheMonitor(
            IOptions<ClusterOptions> clusterOptions,
            IOptions<SiloOptions> siloOptions,
            string queueId,
            string providerName)
            : this(
                clusterOptions.Value.ClusterId ?? "unknown",
                siloOptions.Value.SiloName ?? "unknown",
                queueId,
                providerName)
        {
        }

        /// <summary>
        /// Creates the dimensions array with proper labeling for Aevatar metrics.
        /// Includes cluster_id, silo_id, queue_id, and provider_name labels as required
        /// by the core-metrics-design.md specification.
        /// </summary>
        /// <param name="clusterId">The cluster identifier</param>
        /// <param name="siloId">The silo identifier</param>
        /// <param name="queueId">The queue identifier</param>
        /// <param name="providerName">The stream provider name</param>
        /// <returns>Array of key-value pairs for metric dimensions</returns>
        private static KeyValuePair<string, object>[] CreateDimensions(
            string clusterId,
            string siloId,
            string queueId,
            string providerName)
        {
            return new[]
            {
                new KeyValuePair<string, object>("cluster_id", clusterId ?? "unknown"),
                new KeyValuePair<string, object>("silo_id", siloId ?? "unknown"),
                new KeyValuePair<string, object>("queue_id", queueId ?? "unknown"),
                new KeyValuePair<string, object>("provider_name", providerName ?? "unknown")
            };
        }
    }

    /// <summary>
    /// Factory for creating AevatarStreamCacheMonitor instances with proper dependency injection.
    /// </summary>
    public interface IAevatarStreamCacheMonitorFactory
    {
        /// <summary>
        /// Creates a cache monitor for the specified queue and provider.
        /// </summary>
        /// <param name="queueId">The queue identifier</param>
        /// <param name="providerName">The stream provider name</param>
        /// <returns>A configured cache monitor instance</returns>
        ICacheMonitor CreateCacheMonitor(string queueId, string providerName);
    }

    /// <summary>
    /// Default implementation of IAevatarStreamCacheMonitorFactory.
    /// </summary>
    public class AevatarStreamCacheMonitorFactory : IAevatarStreamCacheMonitorFactory
    {
        private readonly IOptions<ClusterOptions> _clusterOptions;
        private readonly IOptions<SiloOptions> _siloOptions;

        public AevatarStreamCacheMonitorFactory(
            IOptions<ClusterOptions> clusterOptions,
            IOptions<SiloOptions> siloOptions)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _siloOptions = siloOptions ?? throw new ArgumentNullException(nameof(siloOptions));
        }

        public ICacheMonitor CreateCacheMonitor(string queueId, string providerName)
        {
            return new AevatarStreamCacheMonitor(_clusterOptions, _siloOptions, queueId, providerName);
        }
    }
} 