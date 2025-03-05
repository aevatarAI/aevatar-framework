using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;

namespace Aevatar.PermissionManagement;

public class PermissionCheckFilter : IIncomingGrainCallFilter
{
    private readonly ILogger<PermissionCheckFilter> _logger;
    private IPermissionChecker _permissionChecker;

    public PermissionCheckFilter(IPermissionChecker permissionChecker, ILogger<PermissionCheckFilter> logger)
    {
        _permissionChecker = permissionChecker;
        _logger = logger;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var method = context.ImplementationMethod;
        var declaringType = method.DeclaringType!;
        var classPermissions = declaringType.GetCustomAttributes<PermissionAttribute>(inherit: true);
        var methodPermissions = method.GetCustomAttributes<PermissionAttribute>(inherit: true);

        var allPermissionNames = classPermissions.Concat(methodPermissions)
            .Select(attr => attr.Name)
            .Distinct()
            .ToList();

        if (allPermissionNames.Count == 0)
        {
            await context.Invoke();
            return;
        }

        if (RequestContext.Get("CurrentUser") is not UserContext currentUser)
        {
            throw new AuthenticationException("Request requires authentication");
        }

        var principal = BuildClaimsPrincipal(currentUser);

        _logger.LogInformation("Start permission checking for method {MethodName}, permission name is {PermissionName}", method.Name, allPermissionNames.FirstOrDefault());

        foreach (var permissionName in allPermissionNames)
        {
            if (!await _permissionChecker.IsGrantedAsync(principal, permissionName))
            {
                throw new AuthenticationException(
                    $"Missing required permission: {permissionName}, " +
                    $"userId: {currentUser.UserId.ToString()}, " +
                    $"clientId: {currentUser.ClientId}");
            }
        }

        _logger.LogInformation("End permission checking of method {MethodName}", method.Name);

        await context.Invoke();
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(UserContext user)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, user.UserId.ToString()),
            new(AbpClaimTypes.ClientId, user.ClientId)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(AbpClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}