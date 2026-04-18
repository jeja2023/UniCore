namespace Platform.Core.Abstractions;

/// <summary>
/// 缓存 JWT 校验阶段使用的「用户是否启用」结果，降低每请求数据库压力；变更启用状态时必须调用 <see cref="Invalidate"/>。
/// 未启用 Redis 时为进程内缓存；启用 Redis 时为分布式缓存以便多实例一致。
/// </summary>
public interface IJwtUserEnabledValidationCache
{
    bool TryGet(Guid userId, string tenantId, out bool enabled);

    void Set(Guid userId, string tenantId, bool enabled, TimeSpan ttl);

    void Invalidate(Guid userId, string tenantId);
}
