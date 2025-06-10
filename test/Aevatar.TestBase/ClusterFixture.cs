using System.Collections.Immutable;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Aevatar.Core.Tests;
using Aevatar.Extensions;
using Aevatar.PermissionManagement.Extensions;
using Aevatar.Plugins;
using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Repositories;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Orleans.Metadata;
using Orleans.TestingHost;
using Volo.Abp.AutoMapper;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Reflection;

namespace Aevatar.TestBase;

public class ClusterFixture : IDisposable, ISingletonDependency
{
    public static MockLoggerProvider LoggerProvider { get; set; }

    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurations>();
        builder.AddClientBuilderConfigurator<TestClientBuilderConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

    public TestCluster Cluster { get; private set; }

    private class TestSiloConfigurations : ISiloConfigurator
    {
        public void Configure(ISiloBuilder hostBuilder)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.secrets.json", true)
                .Build();

            hostBuilder
                .ConfigureServices(services =>
                {
                    services.AddAutoMapper(typeof(AevatarTestBaseModule).Assembly);

                    var mock = new Mock<ILocalEventBus>();
                    services.AddSingleton(typeof(ILocalEventBus), mock.Object);

                    // Configure logging
                    var loggerProvider = new MockLoggerProvider("Aevatar");
                    services.AddSingleton<ILoggerProvider>(loggerProvider);
                    LoggerProvider = loggerProvider;
                    services.AddLogging(logging =>
                    {
                        logging.AddConsole(); // Adds console logger
                    });
                    services.OnExposing(onServiceExposingContext =>
                    {
                        var implementedTypes = ReflectionHelper.GetImplementedGenericTypes(
                            onServiceExposingContext.ImplementationType,
                            typeof(IObjectMapper<,>)
                        );
                    });

                    services.AddTransient(typeof(IObjectMapper<>), typeof(DefaultObjectMapper<>));
                    services.AddTransient(typeof(IObjectMapper), typeof(DefaultObjectMapper));
                    services.AddTransient(typeof(IAutoObjectMappingProvider),
                        typeof(AutoMapperAutoObjectMappingProvider));
                    services.AddTransient<IMapperAccessor>(sp => new MapperAccessor()
                    {
                        Mapper = sp.GetRequiredService<IMapper>()
                    });

                    var grainTypeMap = ImmutableDictionary<GrainType, Type>.Empty;
                    var gAgentType = typeof(IGAgent);
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var gAgentTypes = new List<Type>();
                    foreach (var assembly in assemblies)
                    {
                        var types = assembly.GetTypesIgnoringLoadException()
                            .Where(t => gAgentType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
                        gAgentTypes.AddRange(types);
                    }
                    var serviceProvider = services.BuildServiceProvider();
                    var grainTypeResolver = serviceProvider.GetRequiredService<GrainTypeResolver>();
                    foreach (var type in gAgentTypes)
                    {
                        var grainType = grainTypeResolver.GetGrainType(type);
                        if (!grainTypeMap.ContainsKey(grainType))
                        {
                            grainTypeMap = grainTypeMap.Add(grainType, type);
                        }
                    }
                    services.AddSingleton(grainTypeMap);
                    services.AddSingleton<IStateProjector, TestStateProjector>();
                    services.AddSingleton<IStateDispatcher, StateDispatcher>();
                    
                    services.Configure<PluginGAgentLoadOptions>(services.GetConfiguration().GetSection("Plugins"));
                    services.AddTransient<ITenantPluginCodeRepository, TenantPluginCodeRepository>();
                    services.AddTransient<IPluginCodeStorageRepository, PluginCodeStorageRepository>();
                    services.AddTransient<TenantPluginCodeMongoDbContext>();
                    services.AddTransient<PluginCodeStorageMongoDbContext>();
                })
                .UseAevatar()
                .UseAevatarPermissionManagement()
                .AddMemoryStreams("Aevatar")
                .AddMemoryStreams("AevatarSignalR")
                .AddMemoryGrainStorage("PubSubStore")
                .AddMemoryGrainStorageAsDefault()
                .AddLogStorageBasedLogConsistencyProvider("LogStorage");
        }
    }

    public class MapperAccessor : IMapperAccessor
    {
        public IMapper Mapper { get; set; }
    }

    private class TestClientBuilderConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) => clientBuilder
            .AddMemoryStreams("Aevatar");
    }
}