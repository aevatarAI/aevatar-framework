using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Plugin;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Configuration;
using Orleans.Metadata;
using Orleans.Runtime.Hosting;
using Orleans.Serialization;
using Orleans.Storage;

namespace Aevatar.Plugins.Extensions;

public static class OrleansHostExtensions
{
    public static ISiloBuilder UseAevatarPlugins(this ISiloBuilder siloBuilder)
    {
        return siloBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<ApplicationPartManager>();
            services.AddSingleton<PluginGAgentManager>();
            services.AddSingleton<ILifecycleParticipant<ISiloLifecycle>>(sp =>
                sp.GetRequiredService<PluginGAgentManager>());
        });
    }

    public static IClientBuilder UseAevatarPlugins(this IClientBuilder clientBuilder)
    {
        return clientBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<ApplicationPartManager>();
            services.AddSingleton<IPluginGAgentManager, PluginGAgentManager>();
        });
    }
}