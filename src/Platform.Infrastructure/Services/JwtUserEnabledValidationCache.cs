using Microsoft.Extensions.Caching.Memory;
using Platform.Core.Abstractions;

namespace Platform.Infrastructure.Services;

public sealed class JwtUserEnabledValidationCache(IMemoryCache memoryCache) : IJwtUserEnabledValidationCache
{
    private static string CacheKey(Guid userId, string tenantId) => $"unicore:jwt-user-enabled:{tenantId}:{userId:N}";

    public bool TryGet(Guid userId, string tenantId, out bool enabled)
    {
        if (memoryCache.TryGetValue(CacheKey(userId, tenantId), out bool value))
        {
            enabled = value;
            return true;
        }

        enabled = false;
        return false;
    }

    public void Set(Guid userId, string tenantId, bool enabled, TimeSpan ttl) =>
        memoryCache.Set(
            CacheKey(userId, tenantId),
            enabled,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });

    public void Invalidate(Guid userId, string tenantId) =>
        memoryCache.Remove(CacheKey(userId, tenantId));
}
