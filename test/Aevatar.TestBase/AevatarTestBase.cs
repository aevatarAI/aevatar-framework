using System.Reflection;
using Orleans.TestingHost;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace Aevatar.TestBase;

public abstract class AevatarTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected readonly TestCluster Cluster;

    protected AevatarTestBase() 
    {
        Cluster = GetRequiredService<ClusterFixture>().Cluster;
    }
}