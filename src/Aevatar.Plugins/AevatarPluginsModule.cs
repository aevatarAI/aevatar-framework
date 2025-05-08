using Aevatar.Plugins.DbContexts;
using Aevatar.Plugins.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace Aevatar.Plugins;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAutoMapperModule)
)]
public class AevatarPluginsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<PluginGAgentLoadOptions>(options =>
        {
            var tenantIdStr = configuration.GetSection("Plugins:TenantId").Value;
            if (tenantIdStr == null) return;
            if (Guid.TryParse(tenantIdStr, out var tenantId))
            {
                options.TenantId = tenantId;
            }

            var hostId = configuration.GetValue<string?>("Host:HostId");
            options.HostId = hostId;
        });
        context.Services.AddTransient<ITenantPluginCodeRepository, TenantPluginCodeRepository>();
        context.Services.AddTransient<IPluginCodeStorageRepository, PluginCodeStorageRepository>();
        context.Services.AddTransient<IPluginLoadStatusRepository, PluginLoadStatusRepository>();
        context.Services.AddTransient<TenantPluginCodeMongoDbContext>();
        context.Services.AddTransient<PluginCodeStorageMongoDbContext>();
        context.Services.AddTransient<PluginLoadStatusMongoDbContext>();
    }
}