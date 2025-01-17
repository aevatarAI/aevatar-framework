using Aevatar.AI.BrainFactory;
using Aevatar.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volo.Abp.AutoMapper;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;

namespace Aevatar.AI.AIGAgent.Tests;

[DependsOn(
    typeof(AevatarTestBaseModule),
    typeof(AbpEventBusModule)
)]
public class AevatarGAgentsTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AevatarGAgentsTestModule>(); });
        
        var brainFactoryMock = new Mock<IBrainFactory>();
        // Configure your mock here if needed
        
        context.Services.AddSingleton(brainFactoryMock.Object);
    }
}