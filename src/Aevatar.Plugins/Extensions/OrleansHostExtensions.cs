using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

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
}