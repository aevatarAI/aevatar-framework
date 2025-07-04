using Aevatar.EventSourcing.MongoDB.Options;
using Microsoft.Extensions.Options;

namespace Aevatar.EventSourcing.MongoDB.Hosting;

public static class MongoDbStorageSiloBuilderExtensions
{
    public static ISiloBuilder AddMongoDbStorageBasedLogConsistencyProvider(this ISiloBuilder builder, string name,
        Action<MongoDbStorageOptions> configureOptions)
    {
        return builder.ConfigureServices(service =>
            service.AddMongoDbBasedLogConsistencyProvider(name, configureOptions));
    }

    public static ISiloBuilder AddMongoDbStorageBasedLogConsistencyProvider(this ISiloBuilder builder,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return builder.ConfigureServices(service =>
            service.AddMongoDbBasedLogConsistencyProvider("LogStorage", configureOptions));
    }

    public static ISiloBuilder AddMongoDbStorageBasedLogConsistencyProvider(this ISiloBuilder builder, string name,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return builder.ConfigureServices(service =>
            service.AddMongoDbBasedLogConsistencyProvider(name, configureOptions));
    }

    /// <summary>
    /// Adds Orleans-compatible MongoDB provider that can handle Orleans LogStateWithMetaData format
    /// </summary>
    public static ISiloBuilder AddOrleansCompatibleMongoDbBasedLogConsistencyProvider(this ISiloBuilder builder, string name,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return builder.ConfigureServices(service =>
            service.AddOrleansCompatibleMongoDbBasedLogConsistencyProvider(name, configureOptions));
    }

    /// <summary>
    /// Adds Orleans-compatible MongoDB provider as default
    /// </summary>
    public static ISiloBuilder AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(this ISiloBuilder builder,
        Action<OptionsBuilder<MongoDbStorageOptions>>? configureOptions = null)
    {
        return builder.ConfigureServices(service =>
            service.AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(configureOptions));
    }
}