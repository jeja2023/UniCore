using Microsoft.Extensions.Caching.Distributed;
using Platform.Core.Abstractions;

namespace Platform.Infrastructure.Services;

/// <summary>
/// 使用 <see cref="IDistributedCache"/>（Redis 启用时）在多实例间共享 JWT 校验阶段的「用户是否启用」缓存。
/// </summary>
public sealed class DistributedJwtUserEnabledValidationCache(IDistributedCache cache) : IJwtUserEnabledValidationCache
{
    private static string CacheKey(Guid userId, string tenantId) =>
        $"unicore:jwt-user-enabled:{tenantId}:{userId:N}";

    public bool TryGet(Guid userId, string tenantId, out bool enabled)
    {
        var bytes = cache.Get(CacheKey(userId, tenantId));
        if (bytes is null || bytes.Length == 0)
        {
            enabled = false;
            return false;
        }

        enabled = bytes[0] != 0;
        return true;
    }

    public void Set(Guid userId, string tenantId, bool enabled, TimeSpan ttl) =>
        cache.Set(
            CacheKey(userId, tenantId),
            [(byte)(enabled ? 1 : 0)],
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });

    public void Invalidate(Guid userId, string tenantId) =>
        cache.Remove(CacheKey(userId, tenantId));
}
