using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Repositories;
using Aevatar.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.AutoMapper;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace Aevatar.GAgents.Tests;

[DependsOn(
    typeof(AevatarTestBaseModule),
    typeof(AbpEventBusModule),
    typeof(AbpPermissionManagementDomainModule)
)]
public class AevatarGAgentsTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        Configure<PermissionManagementOptions>(options =>
        {
        });
        Configure<AbpPermissionOptions>(options =>
        {
            options.DefinitionProviders.Add<TestPermissionDefinitionProvider>();
        });
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AevatarGAgentsTestModule>(); });
        context.Services.AddTransient<IPermissionGrantRepository, MockPermissionGrantRepository>();
        context.Services.AddSingleton<IPluginCodeStorageRepository, InMemoryPluginCodeStorageRepository>();
        context.Services.AddSingleton<ITenantPluginCodeRepository, InMemoryTenantPluginCodeRepository>();
        context.Services.AddSingleton<IPluginLoadStatusRepository, InMemoryPluginLoadStatusRepository>();
    }
}