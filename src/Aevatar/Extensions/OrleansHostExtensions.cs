using System.Reflection;
using Aevatar.Core;
using Aevatar.Plugins.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Orleans.Serialization;
using Orleans.SyncWork;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace Aevatar.Extensions;

public static class OrleansHostExtensions
{
    public static ISiloBuilder UseAevatar(this ISiloBuilder builder, bool includingAbpServices = false)
    {
        var abpApplication = AbpApplicationFactory.Create<AevatarModule>();
        abpApplication.Initialize();

        return builder
            .ConfigureServices(services =>
            {
                AsyncHelper.RunSync(() => LoadPluginsAsync(services, abpApplication));
                if (includingAbpServices)
                {
                    services.Add(abpApplication.Services);
                }

                services.AddSingleton(_ =>
                    new LimitedConcurrencyLevelTaskScheduler(AevatarGAgentConstants.MaxSyncWorkConcurrency));
            });
    }

    public static ISiloBuilder UseAevatar<TAbpModule>(this ISiloBuilder builder) where TAbpModule : AbpModule
    {
        var abpApplication = AbpApplicationFactory.Create<TAbpModule>();
        abpApplication.Initialize();

        return builder
            .UseAevatar()
            .ConfigureServices(services =>
            {
                services.Add(abpApplication.Services);
            });
    }

    private static async Task LoadPluginsAsync(IServiceCollection services,
        IAbpApplicationWithInternalServiceProvider application)
    {
        var assemblies = await application.GetTenantPluginAssemblyListAsync();
        services.AddSerializer(options =>
        {
            foreach (var assembly in assemblies)
            {
                options.AddAssembly(assembly);
            }
        });

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);
            return assembly != null ? assembly : null;
        };
    }

    public static IClientBuilder UseAevatar(this IClientBuilder builder, bool includingAbpServices = false)
    {
        var abpApplication = AbpApplicationFactory.Create<AevatarModule>();
        abpApplication.Initialize();

        return builder
            .ConfigureServices(services =>
        {
            AsyncHelper.RunSync(() => LoadPluginsAsync(services, abpApplication));
            if (includingAbpServices)
            {
                services.Add(abpApplication.Services);
            }
        });
    }
}