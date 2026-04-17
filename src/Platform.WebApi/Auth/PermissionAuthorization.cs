using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Platform.Identity.Services;
using System.Security.Claims;

namespace Platform.WebApi.Auth;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(
    CurrentUserContextService currentUserContextService,
    IMemoryCache memoryCache) : AuthorizationHandler<PermissionRequirement>
{
    private static readonly TimeSpan PermissionCacheTtl = TimeSpan.FromSeconds(30);

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

        var normalizedRoles = roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedRoles.Length == 0)
        {
            return;
        }

        var cacheKey = $"permission:{tenantId}:{string.Join('|', normalizedRoles)}:{requirement.PermissionCode}";
        if (!memoryCache.TryGetValue(cacheKey, out bool permitted))
        {
            permitted = await currentUserContextService.HasPermissionAsync(
                tenantId,
                normalizedRoles,
                requirement.PermissionCode);
            memoryCache.Set(cacheKey, permitted, PermissionCacheTtl);
        }

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
