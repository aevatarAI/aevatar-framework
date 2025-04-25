using Aevatar.Core;
using Aevatar.Core.Abstractions;
 using Aevatar.Core.Abstractions.Plugin;
using Aevatar.PermissionManagement;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace Aevatar.TestBase;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AevatarModule),
    typeof(AevatarPermissionManagementModule)
)]
public class AevatarTestBaseModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAuditingOptions>(options => { options.IsEnabled = false; });
        context.Services.AddSingleton<ClusterFixture>();
        context.Services.AddSingleton<IClusterClient>(sp => context.Services.GetRequiredService<ClusterFixture>().Cluster.Client);
        context.Services.AddSingleton<IGrainFactory>(sp => context.Services.GetRequiredService<ClusterFixture>().Cluster.GrainFactory);
        context.Services.AddSingleton<IGAgentFactory>(sp => new GAgentFactory(context.Services.GetRequiredService<ClusterFixture>().Cluster.Client));
        context.Services.AddSingleton<IGAgentManager>(sp => new GAgentManager(context.Services.GetRequiredService<ClusterFixture>().Cluster.Client, context.Services.GetRequiredService<IPluginGAgentManager>()));
        context.Services.AddSingleton<IEventDispatcher, DefaultEventDispatcher>();
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AevatarTestBaseModule>(); });
    }
}
