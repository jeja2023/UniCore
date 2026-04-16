using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence.Entities;

namespace Platform.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext dbContext,
        bool recreateOnStartup = false,
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
        admin.PasswordHash = hasher.HashPassword(admin, "UniCore@123");

        dbContext.Tenants.Add(defaultTenant);
        dbContext.Roles.AddRange(adminRole, opsRole);
        dbContext.Users.Add(admin);
        dbContext.UserRoles.Add(new UserRoleEntity { UserId = admin.UserId, RoleId = adminRole.RoleId });
        dbContext.RolePermissions.AddRange(
            new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = "user.read" },
            new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = "user.create" },
            new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = "user.update" },
            new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = "permission.read" },
            new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = "permission.update" },
            new RolePermissionEntity { RoleId = adminRole.RoleId, PermissionCode = "audit.read" },
            new RolePermissionEntity { RoleId = opsRole.RoleId, PermissionCode = "audit.read" }
        );
        dbContext.RoleDataScopes.AddRange(
            new RoleDataScopeEntity { RoleId = adminRole.RoleId, Scope = "Tenant" },
            new RoleDataScopeEntity { RoleId = opsRole.RoleId, Scope = "Self" });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
