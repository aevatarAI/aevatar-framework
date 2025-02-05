using System.Reflection;
using Aevatar;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;
using Serilog;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace PluginGAgent.Silo;

[DependsOn(
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutofacModule),
    typeof(AbpAutoMapperModule),
    typeof(AevatarModule)
)]
public class PluginGAgentTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<PluginGAgentTestModule>(); });
        context.Services.AddHostedService<PluginGAgentTestHostedService>();
        context.Services.AddSerilog(_ => {},
            true, writeToProviders: true);
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IEventDispatcher, DefaultEventDispatcher>();
    }
}