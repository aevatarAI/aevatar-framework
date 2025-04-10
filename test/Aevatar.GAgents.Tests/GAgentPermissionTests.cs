using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.PermissionManagement;
using Aevatar.Plugins.Extensions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;

namespace Aevatar.GAgents.Tests;

public sealed class GAgentPermissionTests : AevatarGAgentsTestBase
{
    private readonly IGAgentFactory _gAgentFactory;

    public GAgentPermissionTests()
    {
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
    }

    [Fact]
    public async Task PermissionInfosProviderTest()
    {
        var allPermissionInfos = GAgentPermissionHelper.GetAllPermissionInfos();
        allPermissionInfos.ShouldContain(i =>
            i.Name == "AbpIdentity.Roles.Create"
            && i.DisplayName == "Only for testing."
            && i.Type == "Aevatar.Core.Tests.TestGAgents.PermissionGAgent"
        );
        allPermissionInfos.ShouldContain(i =>
            i.Name == "DoSomething2"
            && i.GroupName == "DefaultGroup"
        );
        allPermissionInfos.ShouldContain(i =>
            i.Name == "DoSomething3"
            && i.GroupName == "AnotherGroup"
        );
    }
    
    [Fact]
    public async Task PermissionCheckTest()
    {
        var permissionGAgent = await _gAgentFactory.GetGAgentAsync<IPermissionGAgent>();

        var userContext = new UserContext()
        {
            UserId = "testUser".ToGuid(),
            Roles = new []{"admin"},
            UserName = "testUser",
            Email = "testUser@abp.io",
            ClientId = ""
        };
        RequestContext.Set("CurrentUser", userContext);
        var exception = await Assert.ThrowsAsync<NullReferenceException>(() => permissionGAgent.DoSomething1Async());
    }
}