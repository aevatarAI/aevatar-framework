using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using System;

using Aevatar.EventSourcing.MongoDB.Options;

namespace Aevatar.EventSourcing.MongoDB.Configuration
{
    internal class MongoDBGrainStorageConfigurator : IPostConfigureOptions<MongoDbStorageOptions>
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultStateProviderSerializerOptionsConfigurator{TOptions}"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        public MongoDBGrainStorageConfigurator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public void PostConfigure(string name, MongoDbStorageOptions options)
        {
            if (options.GrainStateSerializer == default)
            {
                // First, try to get a IGrainStateSerializer that was registered with the same name as the State provider
                // If none is found, fallback to system wide default
                options.GrainStateSerializer = _serviceProvider.GetKeyedService<IGrainStateSerializer>(name) ?? _serviceProvider.GetRequiredService<IGrainStateSerializer>();
            }
        }
    }
}
