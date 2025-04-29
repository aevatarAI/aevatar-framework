using Aevatar.TestBase;

namespace Aevatar.GAgents.Tests;

[CollectionDefinition("PluginTests", DisableParallelization = true)]
public class PluginTestCollection : ICollectionFixture<ClusterFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
} 