using Volo.Abp.Authorization.Permissions;

namespace Aevatar.GAgents.Tests;

public class TestPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var testGroup = context.AddGroup("TestGroup");

        testGroup.AddPermission(
            name: "DoSomething1",
            isEnabled: true
        );
        testGroup.AddPermission(
            name: "DoSomething2",
            isEnabled: true
        );
        testGroup.AddPermission(
            name: "DoSomething3",
            isEnabled: true
        );
    }
}