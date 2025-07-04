using Aevatar.EventSourcing.Core.LogConsistency;
using Aevatar.EventSourcing.Core.Storage;
using Aevatar.EventSourcing.MongoDB.Options;
using Aevatar.EventSourcing.MongoDB.Configuration;
using Aevatar.EventSourcing.MongoDB.Serializers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Storage;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;

namespace Aevatar.EventSourcing.MongoDB.Hosting;

public static class MongoDbStorageServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDbBasedLogConsistencyProviderAsDefault(this IServiceCollection services,
        Action<MongoDbStorageOptions> configureOptions)
    {
        return services.AddMongoDbBasedLogConsistencyProvider(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME,
            ob => ob.Configure(configureOptions));
    }

    public static IServiceCollection AddMongoDbBasedLogConsistencyProvider(this IServiceCollection services,
        string name, Action<MongoDbStorageOptions> configureOptions)
    {
        return services.AddMongoDbBasedLogConsistencyProvider(name, ob => ob.Configure(configureOptions));
    }

    public static IServiceCollection AddMongoDbBasedLogConsistencyProviderAsDefault(this IServiceCollection services,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return services.AddMongoDbBasedLogConsistencyProvider(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME,
            configureOptions);
    }

    public static IServiceCollection AddMongoDbBasedLogConsistencyProvider(this IServiceCollection services,
        string name, Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        // Configure log storage.
        configureOptions?.Invoke(services.AddOptions<MongoDbStorageOptions>(name));
        services.AddTransient<IConfigurationValidator>(sp =>
            new MongoDbStorageOptionsValidator(
                sp.GetRequiredService<IOptionsMonitor<MongoDbStorageOptions>>().Get(name), name));
        services
            .AddTransient<IPostConfigureOptions<MongoDbStorageOptions>,
                MongoDBGrainStorageConfigurator>();
        services.ConfigureNamedOptionForLogging<MongoDbStorageOptions>(name);
        if (string.Equals(name, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, StringComparison.Ordinal))
        {
            services.TryAddSingleton(sp =>
                sp.GetKeyedService<ILogConsistentStorage>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME));
        }

        // Register event storage (ILogConsistentStorage) - snapshot storage uses Orleans standard providers
        services.AddKeyedSingleton<ILogConsistentStorage>(name, MongoDbLogConsistentStorageFactory.Create);
        
        services.AddSingleton<ILifecycleParticipant<ISiloLifecycle>>(
            sp =>
            {
                var participant =
                    (ILifecycleParticipant<ISiloLifecycle>)sp.GetRequiredKeyedService<ILogConsistentStorage>(name);
                return participant;
            });

        // Configure log consistency.
        services.TryAddSingleton<Factory<IGrainContext, ILogConsistencyProtocolServices>>(serviceProvider =>
        {
            var protocolServicesFactory =
                ActivatorUtilities.CreateFactory(typeof(DefaultProtocolServices), [typeof(IGrainContext)]);
            return grainContext =>
                (ILogConsistencyProtocolServices)protocolServicesFactory(serviceProvider,
                    [grainContext]);
        });

        // Configure log view adaptor.
        if (string.Equals(name, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, StringComparison.Ordinal))
        {
            services.TryAddSingleton(sp =>
                sp.GetKeyedService<ILogViewAdaptorFactory>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME));
        }

        services.AddKeyedSingleton(name, LogConsistencyProviderFactory.Create);
        return services;
    }

    /// <summary>
    /// Adds MongoDB-based log consistency provider with backward compatibility for Memory storage format
    /// </summary>
    public static IServiceCollection AddCompatibleMongoDbBasedLogConsistencyProvider(this IServiceCollection services,
        string name, Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        // Register the compatible serializer
        services.AddKeyedSingleton<IGrainStateSerializer>(name, (sp, key) => new CompatibleGrainSerializer());
        
        // Use the standard MongoDB provider setup
        return services.AddMongoDbBasedLogConsistencyProvider(name, configureOptions);
    }

    /// <summary>
    /// Adds MongoDB-based log consistency provider as default with backward compatibility for Memory storage format
    /// </summary>
    public static IServiceCollection AddCompatibleMongoDbBasedLogConsistencyProviderAsDefault(this IServiceCollection services,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return services.AddCompatibleMongoDbBasedLogConsistencyProvider(
            ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, configureOptions);
    }

    /// <summary>
    /// Adds MongoDB-based log consistency provider with backward compatibility and custom serializer configuration
    /// </summary>
    public static IServiceCollection AddCompatibleMongoDbBasedLogConsistencyProvider(this IServiceCollection services,
        string name, Action<MongoDbStorageOptions> configureOptions, bool useCompatibleSerializer = true)
    {
        if (useCompatibleSerializer)
        {
            // Register the compatible serializer
            services.AddKeyedSingleton<IGrainStateSerializer>(name, (sp, key) => new CompatibleGrainSerializer());
        }
        
        return services.AddMongoDbBasedLogConsistencyProvider(name, configureOptions);
    }


    /// <summary>
    /// Adds Orleans-compatible MongoDB provider that can handle Orleans LogStateWithMetaData format
    /// </summary>
    public static IServiceCollection AddOrleansCompatibleMongoDbBasedLogConsistencyProvider(this IServiceCollection services,
        string name, Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        // Register the Orleans-compatible serializer
        services.AddKeyedSingleton<IGrainStateSerializer>(name, (sp, key) => new OrleansCompatibleGrainSerializer());
        
        // Use the standard MongoDB provider setup
        return services.AddMongoDbBasedLogConsistencyProvider(name, configureOptions);
    }

    /// <summary>
    /// Adds Orleans-compatible MongoDB provider as default
    /// </summary>
    public static IServiceCollection AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(this IServiceCollection services,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return services.AddOrleansCompatibleMongoDbBasedLogConsistencyProvider(
            ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, configureOptions);
    }

}