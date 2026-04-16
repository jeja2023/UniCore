using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence;

namespace Platform.WebApi.Auth;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(AppDbContext dbContext) : AuthorizationHandler<PermissionRequirement>
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

        var roleCodeSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roleIds = (await dbContext.Roles
                .Select(r => new { r.RoleId, r.RoleCode })
                .ToListAsync())
            .Where(r => roleCodeSet.Contains(r.RoleCode))
            .Select(r => r.RoleId)
            .ToHashSet();

        if (roleIds.Count == 0)
        {
            return;
        }

        var permitted = await dbContext.RolePermissions
            .AnyAsync(rp => roleIds.Contains(rp.RoleId) && rp.PermissionCode == requirement.PermissionCode);

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
}
