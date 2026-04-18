using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Common;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using Platform.Infrastructure.Services;
using System.Text.RegularExpressions;

namespace Platform.Permission.Services;

public sealed class PermissionService(
    AppDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    DataScopeService dataScopeService,
    PermissionVersionService permissionVersionService)
{
    private static readonly Regex PermissionCodePattern = new("^[a-z][a-z0-9]*\\.[a-z][a-z0-9]*$", RegexOptions.Compiled);
    public async Task<IReadOnlyCollection<string>> GetPermissionsByRoleAsync(string role, CancellationToken cancellationToken = default) =>
        await (from r in dbContext.Roles
               join rp in dbContext.RolePermissions on r.RoleId equals rp.RoleId
               where r.TenantId == tenantContextAccessor.TenantId && r.RoleCode == role
               select rp.PermissionCode)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Roles
            .Where(x => x.TenantId == tenantContextAccessor.TenantId)
            .OrderBy(x => x.RoleCode)
            .Select(x => new RoleDto(x.RoleId, x.RoleCode, x.RoleName))
            .ToListAsync(cancellationToken);

    public async Task<RoleDto> CreateRoleAsync(string roleCode, string roleName, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var exists = await dbContext.Roles.AnyAsync(x => x.TenantId == tenantId && x.RoleCode == roleCode, cancellationToken);
        if (exists)
        {
            throw new AppException(ErrorCodes.ValidationError, $"角色编码已存在: {roleCode}", 409);
        }

        var role = new RoleEntity
        {
            RoleId = Guid.NewGuid(),
            TenantId = tenantId,
            RoleCode = roleCode,
            RoleName = roleName
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        await permissionVersionService.BumpAsync(tenantId, "role.created", cancellationToken: cancellationToken);
        return new RoleDto(role.RoleId, role.RoleCode, role.RoleName);
    }

    public async Task<IReadOnlyCollection<string>> GrantPermissionsAsync(string roleCode, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default)
    {
        var invalidCodes = permissions.Where(p => !PermissionCodePattern.IsMatch(p)).Distinct().ToArray();
        if (invalidCodes.Length > 0)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"权限点命名不合法（必须是 module.action 格式，小写字母数字）：{string.Join(", ", invalidCodes)}");
        }

        var role = await dbContext.Roles.SingleOrDefaultAsync(x => x.TenantId == tenantContextAccessor.TenantId && x.RoleCode == roleCode, cancellationToken);
        if (role is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"角色不存在: {roleCode}", 404);
        }

        var currentPermissions = await dbContext.RolePermissions
            .Where(x => x.RoleId == role.RoleId)
            .Select(x => x.PermissionCode)
            .ToListAsync(cancellationToken);

        var toAdd = permissions.Except(currentPermissions).Select(p => new RolePermissionEntity { RoleId = role.RoleId, PermissionCode = p });
        dbContext.RolePermissions.AddRange(toAdd);
        await dbContext.SaveChangesAsync(cancellationToken);
        await permissionVersionService.BumpAsync(tenantContextAccessor.TenantId, $"role.permissions.granted:{roleCode}", cancellationToken: cancellationToken);

        return await GetPermissionsByRoleAsync(roleCode, cancellationToken);
    }

    public Task SetRoleDataScopeAsync(string roleCode, DataScope scope, string? customExpression = null, string changedBy = "system", int? expectedRevision = null, CancellationToken cancellationToken = default) =>
        dataScopeService.SetRoleScopeAsync(roleCode, tenantContextAccessor.TenantId, scope, customExpression, changedBy, expectedRevision, cancellationToken);

    public Task<RoleDataScopeDetail?> GetRoleDataScopeAsync(string roleCode, CancellationToken cancellationToken = default) =>
        dataScopeService.GetRoleScopeAsync(roleCode, tenantContextAccessor.TenantId, cancellationToken);

    public DataScopeExpressionValidationResult ValidateRoleDataScopeExpression(string? customExpression) =>
        dataScopeService.ValidateCustomExpression(customExpression);

    public string ComposeRoleDataScopeExpression(IReadOnlyCollection<DataScopeExpressionRule> rules) =>
        dataScopeService.ComposeCustomExpression(rules);

    public Task<IReadOnlyCollection<RoleDataScopeHistoryItem>> GetRoleDataScopeHistoryAsync(string roleCode, int take = 20, CancellationToken cancellationToken = default) =>
        dataScopeService.GetRoleScopeHistoryAsync(roleCode, tenantContextAccessor.TenantId, take, cancellationToken);

    public Task<RoleDataScopeDetail> RollbackRoleDataScopeAsync(string roleCode, int targetVersion, string changedBy, CancellationToken cancellationToken = default) =>
        dataScopeService.RollbackRoleScopeAsync(roleCode, tenantContextAccessor.TenantId, targetVersion, changedBy, cancellationToken);

    public Task<RoleDataScopeDiffResult> DiffRoleDataScopeVersionsAsync(string roleCode, int fromVersion, int toVersion, CancellationToken cancellationToken = default) =>
        dataScopeService.DiffRoleScopeVersionsAsync(roleCode, tenantContextAccessor.TenantId, fromVersion, toVersion, cancellationToken);

    public DataScopeParseResult ParseRoleDataScopeExpression(string? customExpression) =>
        dataScopeService.ParseExpression(customExpression);

}

public sealed record RoleDto(Guid RoleId, string RoleCode, string RoleName);
