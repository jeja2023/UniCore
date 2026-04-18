using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Platform.Core.Abstractions;
using Platform.Core.Common;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using Platform.Infrastructure.Services;

namespace Platform.Identity.Services;

public sealed class UserService(
    AppDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    DataScopeService dataScopeService,
    EntityChangeAuditService entityChangeAuditService,
    IJwtUserEnabledValidationCache jwtUserEnabledCache)
{
    private readonly PasswordHasher<UserEntity> _passwordHasher = new();

    public async Task<IReadOnlyCollection<UserDto>> GetUsersAsync(string requesterId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        if (!Guid.TryParse(requesterId, out var requesterGuid))
        {
            throw new AppException(ErrorCodes.ValidationError, $"用户ID格式错误: {requesterId}");
        }

        var scope = await dataScopeService.ResolveScopeAsync(requesterGuid, tenantId, cancellationToken);
        var query = dataScopeService.ApplyUserFilter(dbContext.Users, scope, requesterGuid, tenantId);

        return await query
            .OrderBy(x => x.Username)
            .Select(x => new UserDto(x.UserId.ToString(), x.TenantId, x.DepartmentCode, x.Username, x.DisplayName, x.Enabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new AppException(ErrorCodes.ValidationError, $"用户ID格式错误: {userId}");
        }

        var tenantId = tenantContextAccessor.TenantId;
        return await (from ur in dbContext.UserRoles
                      join r in dbContext.Roles on ur.RoleId equals r.RoleId
                      where ur.UserId == userGuid && r.TenantId == tenantId
                      select r.RoleCode)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> AssignRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new AppException(ErrorCodes.ValidationError, $"用户ID格式错误: {userId}");
        }

        var tenantId = tenantContextAccessor.TenantId;
        var userExists = await dbContext.Users.AnyAsync(x => x.TenantId == tenantId && x.UserId == userGuid, cancellationToken);
        if (!userExists)
        {
            throw new AppException(ErrorCodes.NotFound, $"用户不存在: {userId}", 404);
        }

        var roleEntities = await dbContext.Roles.Where(x => x.TenantId == tenantId && roles.Contains(x.RoleCode)).ToListAsync(cancellationToken);
        var missingRoles = roles.Except(roleEntities.Select(x => x.RoleCode)).ToArray();
        if (missingRoles.Length > 0)
        {
            throw new AppException(ErrorCodes.ValidationError, $"角色不存在: {string.Join(", ", missingRoles)}");
        }

        var currentRoleIds = await dbContext.UserRoles
            .Where(x => x.UserId == userGuid)
            .Join(
                dbContext.Roles.Where(r => r.TenantId == tenantId),
                ur => ur.RoleId,
                role => role.RoleId,
                (ur, _) => ur.RoleId)
            .ToArrayAsync(cancellationToken);

        var toAdd = roleEntities
            .Where(x => !currentRoleIds.Contains(x.RoleId))
            .Select(x => new UserRoleEntity { UserId = userGuid, RoleId = x.RoleId });
        dbContext.UserRoles.AddRange(toAdd);
        await dbContext.SaveChangesAsync(cancellationToken);

        await entityChangeAuditService.RecordAsync(
            entityName: "UserEntity",
            entityId: userGuid.ToString(),
            changes: new Dictionary<string, object?>
            {
                ["roles_assigned"] = roles.ToArray()
            },
            cancellationToken: cancellationToken);

        return await GetUserRolesAsync(userId, cancellationToken);
    }

    public async Task<UserDto> CreateUserAsync(string username, string displayName, string password, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var currentTenantId = string.IsNullOrWhiteSpace(tenantId) ? tenantContextAccessor.TenantId : tenantId.Trim();
        var exists = await dbContext.Users.AnyAsync(x => x.TenantId == currentTenantId && x.Username == username, cancellationToken);
        if (exists)
        {
            throw new AppException(ErrorCodes.ValidationError, $"用户名已存在: {username}", 409);
        }

        var user = new UserEntity
        {
            UserId = Guid.NewGuid(),
            TenantId = currentTenantId,
            DepartmentCode = "default",
            Username = username,
            DisplayName = displayName,
            Enabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new UserDto(user.UserId.ToString(), user.TenantId, user.DepartmentCode, user.Username, user.DisplayName, user.Enabled);
    }

    public async Task<UserDto> SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new AppException(ErrorCodes.ValidationError, $"用户ID格式错误: {userId}");
        }

        var tenantId = tenantContextAccessor.TenantId;
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userGuid, cancellationToken);
        if (user is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"用户不存在: {userId}", 404);
        }

        user.Enabled = enabled;
        if (!enabled)
        {
            // 集成测试使用的 InMemory 提供程序不支持 ExecuteUpdateAsync。
            var tokens = await dbContext.RefreshTokens
                .Where(x => x.UserId == user.UserId && !x.Revoked)
                .ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.Revoked = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        jwtUserEnabledCache.Invalidate(user.UserId, user.TenantId);

        await entityChangeAuditService.RecordAsync(
            entityName: "UserEntity",
            entityId: user.UserId.ToString(),
            changes: new Dictionary<string, object?>
            {
                ["enabled"] = enabled
            },
            cancellationToken: cancellationToken);

        return new UserDto(user.UserId.ToString(), user.TenantId, user.DepartmentCode, user.Username, user.DisplayName, user.Enabled);
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new AppException(ErrorCodes.ValidationError, $"用户ID格式错误: {userId}");
        }

        var tenantId = tenantContextAccessor.TenantId;
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userGuid, cancellationToken);
        if (user is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"用户不存在: {userId}", 404);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await dbContext.SaveChangesAsync(cancellationToken);

        await entityChangeAuditService.RecordAsync(
            entityName: "UserEntity",
            entityId: user.UserId.ToString(),
            changes: new Dictionary<string, object?>
            {
                ["password_reset"] = true
            },
            cancellationToken: cancellationToken);
    }
}

public sealed record UserDto(string UserId, string TenantId, string DepartmentCode, string Username, string DisplayName, bool Enabled);
