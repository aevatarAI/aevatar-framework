using Aevatar.Core.Abstractions;
using Aevatar.TestKit;
using Microsoft.Extensions.DependencyInjection;

namespace Aevatar.Core.Tests;

public class AevatarTestKitSilo : TestKitSilo
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IGAgentFactory, GAgentFactory>();
        services.AddTransient<IGAgentManager, GAgentManager>();
    }
}