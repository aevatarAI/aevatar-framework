using OrleansTestKit.Tests.Grains;
using Xunit;

namespace Aevatar.TestKit.Tests;

public class ContextConstructorGrainTests : DefaultTestKitBase
{
    [Fact]
    public async Task CanAccess_GrainContext_InConstructorAsync()
    {
        await Silo.CreateGrainAsync<ContextConstructorGrain>(0);
    }
}
