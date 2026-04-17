using Microsoft.AspNetCore.Authorization;
using Platform.Identity.Services;
using System.Security.Claims;

namespace Platform.WebApi.Auth;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(CurrentUserContextService currentUserContextService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.Claims
            .Where(x => x.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct()
            .ToArray();

        if (roles.Length == 0)
        {
            return;
        }

        var tenantId = context.User.FindFirstValue("tenant_id")?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        var permitted = await currentUserContextService.HasPermissionAsync(
            tenantId,
            roles,
            requirement.PermissionCode);

        if (permitted)
        {
            context.Succeed(requirement);
        }
    }
}

public static class PermissionPolicies
{
    public const string UserRead = "permission:user.read";
    public const string UserCreate = "permission:user.create";
    public const string UserUpdate = "permission:user.update";
    public const string PermissionRead = "permission:permission.read";
    public const string PermissionUpdate = "permission:permission.update";
    public const string AuditRead = "permission:audit.read";
    public const string PlatformFeatureRead = "permission:platform.feature.read";
    public const string PlatformCacheRead = "permission:platform.cache.read";
    public const string PlatformCacheWrite = "permission:platform.cache.write";
    public const string PlatformFileRead = "permission:platform.file.read";
    public const string PlatformFileWrite = "permission:platform.file.write";
}
