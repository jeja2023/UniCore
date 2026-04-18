namespace Platform.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;

public sealed class TenantService(AppDbContext dbContext)
{
    public async Task<IReadOnlyCollection<TenantEntity>> GetTenantsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Tenants.OrderBy(x => x.TenantId).ToListAsync(cancellationToken);

    public async Task<TenantEntity> CreateTenantAsync(string tenantId, string tenantName, CancellationToken cancellationToken = default)
    {
        var normalized = tenantId.Trim();
        var exists = await dbContext.Tenants.AnyAsync(x => x.TenantId == normalized, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"租户已存在: {normalized}");
        }

        var entity = new TenantEntity
        {
            TenantId = normalized,
            TenantName = tenantName.Trim(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Tenants.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = tenantId.Trim();
        var exists = await dbContext.Tenants.AnyAsync(x => x.TenantId == normalized, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"租户不存在: {normalized}");
        }

        var settings = await dbContext.TenantSettings
            .Where(x => x.TenantId == normalized)
            .OrderBy(x => x.SettingKey)
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, cancellationToken);
        return settings;
    }

    public async Task<TenantSettingEntity> UpsertSettingAsync(string tenantId, string settingKey, string settingValue, CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedKey = settingKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new InvalidOperationException("settingKey 不能为空。");
        }
        if (normalizedKey.Length > 128)
        {
            throw new InvalidOperationException("settingKey 长度不能超过 128。");
        }

        var tenantExists = await dbContext.Tenants.AnyAsync(x => x.TenantId == normalizedTenantId, cancellationToken);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"租户不存在: {normalizedTenantId}");
        }

        var entity = await dbContext.TenantSettings.SingleOrDefaultAsync(
            x => x.TenantId == normalizedTenantId && x.SettingKey == normalizedKey,
            cancellationToken);
        if (entity is null)
        {
            entity = new TenantSettingEntity
            {
                TenantSettingId = Guid.NewGuid(),
                TenantId = normalizedTenantId,
                SettingKey = normalizedKey,
                SettingValue = settingValue,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.TenantSettings.Add(entity);
        }
        else
        {
            entity.SettingValue = settingValue;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

public sealed class ExternalIdentityLinkService(AppDbContext dbContext)
{
    public async Task<Guid?> FindUserIdAsync(string tenantId, string provider, string externalUserId, CancellationToken cancellationToken = default)
    {
        var link = await dbContext.ExternalIdentityLinks.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Provider == provider && x.ExternalUserId == externalUserId,
            cancellationToken);
        return link?.UserId;
    }

    public async Task LinkAsync(string tenantId, string provider, string externalUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.ExternalIdentityLinks.AnyAsync(
            x => x.TenantId == tenantId && x.Provider == provider && x.ExternalUserId == externalUserId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.ExternalIdentityLinks.Add(new ExternalIdentityLinkEntity
        {
            ExternalIdentityLinkId = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider,
            ExternalUserId = externalUserId,
            UserId = userId,
            LinkedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
