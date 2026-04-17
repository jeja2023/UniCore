using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence;

namespace Platform.Identity.Services;

public sealed class CurrentUserContextService(AppDbContext dbContext)
{
    public async Task<CurrentUserContextSnapshot?> BuildSnapshotAsync(
        Guid userId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var permissions = await ResolvePermissionsAsync(user.TenantId, roleCodes, cancellationToken);
        return new CurrentUserContextSnapshot(
            user.UserId,
            user.Username,
            user.DisplayName,
            user.TenantId,
            roleCodes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            permissions);
    }

    public async Task<bool> HasPermissionAsync(
        string tenantId,
        IReadOnlyCollection<string> roleCodes,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || roleCodes.Count == 0 || string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        var normalizedRoles = roleCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedRoles.Length == 0)
        {
            return false;
        }

        return await (from role in dbContext.Roles.AsNoTracking()
                      join permission in dbContext.RolePermissions.AsNoTracking() on role.RoleId equals permission.RoleId
                      where role.TenantId == tenantId &&
                            normalizedRoles.Contains(role.RoleCode) &&
                            permission.PermissionCode == permissionCode
                      select permission.PermissionCode)
            .AnyAsync(cancellationToken);
    }

    public async Task<string[]> ResolvePermissionsAsync(
        string tenantId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || roleCodes.Count == 0)
        {
            return [];
        }

        var normalizedRoles = roleCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedRoles.Length == 0)
        {
            return [];
        }

        return await (from role in dbContext.Roles.AsNoTracking()
                      join permission in dbContext.RolePermissions.AsNoTracking() on role.RoleId equals permission.RoleId
                      where role.TenantId == tenantId && normalizedRoles.Contains(role.RoleCode)
                      select permission.PermissionCode)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken);
    }
}

public sealed record CurrentUserContextSnapshot(
    Guid UserId,
    string Username,
    string DisplayName,
    string TenantId,
    string[] Roles,
    string[] Permissions);
