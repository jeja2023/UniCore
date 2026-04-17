using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Security;
using Platform.Infrastructure.Persistence.Entities;

namespace Platform.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext dbContext,
        IReadOnlyList<string> adminPermissionCodes,
        bool recreateOnStartup = false,
        string? adminPassword = null,
        bool allowDefaultAdminPassword = false,
        CancellationToken cancellationToken = default)
    {
        if (recreateOnStartup)
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var defaultTenant = new TenantEntity
        {
            TenantId = "default",
            TenantName = "默认租户",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var adminRole = new RoleEntity { RoleId = Guid.NewGuid(), TenantId = defaultTenant.TenantId, RoleCode = "admin", RoleName = "平台管理员" };
        var opsRole = new RoleEntity { RoleId = Guid.NewGuid(), TenantId = defaultTenant.TenantId, RoleCode = "ops", RoleName = "运维工程师" };

        var hasher = new PasswordHasher<UserEntity>();
        var admin = new UserEntity
        {
            UserId = Guid.NewGuid(),
            TenantId = defaultTenant.TenantId,
            Username = "admin",
            DisplayName = "平台管理员",
            Enabled = true
        };
        var resolvedAdminPassword = string.IsNullOrWhiteSpace(adminPassword)
            ? (allowDefaultAdminPassword
                ? "UniCore@123"
                : throw new InvalidOperationException("Admin seed password is required. Set UNICORE_ADMIN_PASSWORD or Seed:AdminPassword."))
            : adminPassword.Trim();
        admin.PasswordHash = hasher.HashPassword(admin, resolvedAdminPassword);

        dbContext.Tenants.Add(defaultTenant);
        dbContext.Roles.AddRange(adminRole, opsRole);
        dbContext.Users.Add(admin);
        dbContext.UserRoles.Add(new UserRoleEntity { UserId = admin.UserId, RoleId = adminRole.RoleId });
        foreach (var code in adminPermissionCodes)
        {
            dbContext.RolePermissions.Add(new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = code });
        }

        foreach (var code in PlatformPermissionSeed.OpsRolePlatformPermissionCodes)
        {
            dbContext.RolePermissions.Add(new RolePermissionEntity { RoleId = opsRole.RoleId, PermissionCode = code });
        }
        dbContext.RoleDataScopes.AddRange(
            new RoleDataScopeEntity { RoleId = adminRole.RoleId, Scope = "Tenant" },
            new RoleDataScopeEntity { RoleId = opsRole.RoleId, Scope = "Self" });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
