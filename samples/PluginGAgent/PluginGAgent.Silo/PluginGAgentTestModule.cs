using Aevatar;
using Aevatar.Core.Abstractions;
using Aevatar.PermissionManagement;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace PluginGAgent.Silo;

[DependsOn(
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutofacModule),
    typeof(AbpAutoMapperModule),
    // typeof(AevatarPermissionManagementModule),
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
        // Configure<PermissionManagementOptions>(options =>
        // {
        //     options.IsDynamicPermissionStoreEnabled = true;
        // });
    }
}