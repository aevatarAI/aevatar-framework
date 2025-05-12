using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;

namespace Aevatar.PermissionManagement;

public static class GAgentPermissionHelper
{
    public static List<PermissionInfo> GetAllPermissionInfos()
    {
        var permissionInfos = new List<PermissionInfo>();
        var agentType = typeof(IGAgent);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var gAgentTypes = new List<Type>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypesIgnoringLoadException()
                .Where(t => agentType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
            gAgentTypes.AddRange(types);
        }

        foreach (var gAgentType in gAgentTypes)
        {
            var classAttributes = gAgentType.GetCustomAttributes<PermissionAttribute>(inherit: true);
            permissionInfos.AddRange(
                classAttributes.Select(attr => new PermissionInfo
                {
                    Type = gAgentType.FullName!,
                    Name = attr.Name,
                    GroupName = attr.GroupName ?? string.Empty,
                    DisplayName = attr.DisplayName ?? string.Empty
                })
            );

            var methods =
                gAgentType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            permissionInfos.AddRange(
                methods
                    .SelectMany(method => method.GetCustomAttributes<PermissionAttribute>(inherit: true))
                    .Select(attr => new PermissionInfo
                    {
                        Type = gAgentType.FullName!,
                        Name = attr.Name,
                        GroupName = attr.GroupName ?? string.Empty,
                        DisplayName = attr.DisplayName ?? string.Empty
                    })
            );
        }

        return permissionInfos;
    }
}