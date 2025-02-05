using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Plugins;
using Aevatar.Plugins.Extensions;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Serialization;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace Aevatar.Extensions;

public static class OrleansHostExtensions
{
    public static ISiloBuilder UseAevatar(this ISiloBuilder builder)
    {
        var abpApplication = AbpApplicationFactory.Create<AevatarModule>();
        abpApplication.Initialize();

        return builder
            .ConfigureServices(services =>
            {
                foreach (var service in abpApplication.Services)
                {
                    services.Add(service);
                }

                LoadPlugins(services);
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
                foreach (var service in abpApplication.Services)
                {
                    services.Add(service);
                }
            });
    }
    
    private static void LoadPlugins(IServiceCollection services)
    {
        var configuration = services.GetConfiguration();
        var pluginConfig = configuration.GetSection("Plugins");
        var pluginDirectory = pluginConfig["Directory"];
        if (pluginDirectory.IsNullOrEmpty()) return;
        var pluginCodes = PluginLoader.LoadPlugins(pluginDirectory);
        services.AddSerializer(options =>
        {
            foreach (var assembly in pluginCodes.Select(Assembly.Load))
            {
                options.AddAssembly(assembly);
            }
        });
    }

    public static IClientBuilder UseAevatar(this IClientBuilder builder)
    {
        var abpApplication = AbpApplicationFactory.Create<AevatarModule>();
        abpApplication.Initialize();

        return builder
            .ConfigureServices(services =>
        {
            foreach (var service in abpApplication.Services)
            {
                services.Add(service);
            }
        });
    }
}