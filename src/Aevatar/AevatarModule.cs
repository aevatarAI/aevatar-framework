using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Serialization;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace Aevatar;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAutoMapperModule)
)]
public class AevatarModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddConventionalRegistrar(new AevatarDefaultConventionalRegistrar());

    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AevatarModule>(); });
        context.Services.AddSingleton<IGAgentManager, GAgentManager>();
        context.Services.AddSingleton<IGAgentFactory, GAgentFactory>();
        context.Services.AddSingleton<IConfigureGrainTypeComponents, ConfigureAevatarGrainActivator>();
    }
}