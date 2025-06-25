using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Streams;
using Orleans.Streams.Kafka.Config;
using Orleans.Providers;
using System;
using Aevatar.Core.Streaming.Monitors;
using Aevatar.Core.Streaming.Kafka;
using Orleans.Providers.Streams.Common;

namespace Aevatar.Core.Streaming.Extensions
{
    /// <summary>
    /// Extension methods for configuring Aevatar streaming services with monitoring capabilities.
    /// </summary>
    public static class StreamingServiceExtensions
    {
        /// <summary>
        /// Adds Aevatar streaming monitoring services to the service collection.
        /// This includes cache monitors, pressure monitors, and monitored cache factories.
        /// </summary>
        /// <param name="services">The service collection to add services to</param>
        /// <returns>The service collection for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when services is null</exception>
        public static IServiceCollection AddAevatarStreamingMonitoring(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            // Register monitoring factories
            services.TryAddSingleton<IAevatarStreamCacheMonitorFactory, AevatarStreamCacheMonitorFactory>();
            services.TryAddSingleton<IStreamPressureMonitorFactory, StreamPressureMonitorFactory>();
            services.TryAddSingleton<IMonitoredQueueCacheFactory, MonitoredQueueCacheFactory>();

            // Configure default pressure monitoring options
            services.Configure<StreamPressureOptions>(options => {});

            return services;
        }



        /// <summary>
        /// Configures stream pressure monitoring options for a specific provider.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="providerName">The stream provider name</param>
        /// <param name="configureOptions">Configuration action</param>
        /// <returns>The service collection for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or providerName is null</exception>
        /// <exception cref="ArgumentException">Thrown when providerName is empty or whitespace</exception>
        public static IServiceCollection ConfigureStreamPressureMonitoring(
            this IServiceCollection services,
            string providerName,
            Action<StreamPressureOptions> configureOptions)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (providerName == null)
                throw new ArgumentNullException(nameof(providerName));
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("Provider name cannot be empty or whitespace.", nameof(providerName));
            services.Configure<StreamPressureOptions>(providerName, configureOptions);
            return services;
        }

        /// <summary>
        /// Configures global stream pressure monitoring options.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Configuration action</param>
        /// <returns>The service collection for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when services is null</exception>
        public static IServiceCollection ConfigureStreamPressureMonitoring(
            this IServiceCollection services,
            Action<StreamPressureOptions> configureOptions)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            services.Configure<StreamPressureOptions>(configureOptions);
            return services;
        }
    }

    /// <summary>
    /// Extension methods for Orleans silo builder to add Aevatar streaming providers.
    /// </summary>
    public static class SiloBuilderExtensions
    {
        /// <summary>
        /// Adds a monitored Kafka stream provider to the silo builder.
        /// </summary>
        /// <param name="builder">The silo builder</param>
        /// <param name="name">The stream provider name</param>
        /// <param name="configureOptions">Configuration action for Kafka options</param>
        /// <returns>The silo builder for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or name is null</exception>
        /// <exception cref="ArgumentException">Thrown when name is empty or whitespace</exception>
        public static ISiloBuilder AddAevatarKafkaStreaming(
            this ISiloBuilder builder,
            string name,
            Action<KafkaStreamOptions> configureOptions)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Stream provider name cannot be empty or whitespace.", nameof(name));
            return builder.ConfigureServices(services =>
            {
                services.AddAevatarStreamingMonitoring();
            })
            .AddPersistentStreams(name, AevatarKafkaAdapterFactory.Create, b =>
            {
                b.ConfigureStreamPubSub(StreamPubSubType.ExplicitGrainBasedAndImplicit);
                if (configureOptions != null)
                {
                    b.Configure<KafkaStreamOptions>(ob => ob.Configure(configureOptions));
                }
            });
        }


    }

    /// <summary>
    /// Extension methods for Orleans client builder to add Aevatar streaming providers.
    /// </summary>
    public static class ClientBuilderExtensions
    {
        /// <summary>
        /// Adds a monitored Kafka stream provider to the client builder.
        /// </summary>
        /// <param name="builder">The client builder</param>
        /// <param name="name">The stream provider name</param>
        /// <param name="configureOptions">Configuration action for Kafka options</param>
        /// <returns>The client builder for method chaining</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or name is null</exception>
        /// <exception cref="ArgumentException">Thrown when name is empty or whitespace</exception>
        public static IClientBuilder AddAevatarKafkaStreaming(
            this IClientBuilder builder,
            string name,
            Action<KafkaStreamOptions> configureOptions)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Stream provider name cannot be empty or whitespace.", nameof(name));
            return builder.ConfigureServices(services =>
            {
                services.AddAevatarStreamingMonitoring();
            })
            .AddPersistentStreams(name, AevatarKafkaAdapterFactory.Create, b =>
            {
                b.ConfigureStreamPubSub(StreamPubSubType.ExplicitGrainBasedAndImplicit);
                if (configureOptions != null)
                {
                    b.Configure<KafkaStreamOptions>(ob => ob.Configure(configureOptions));
                }
            });
        }


    }

} 