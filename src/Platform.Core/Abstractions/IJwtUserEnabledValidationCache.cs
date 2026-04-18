namespace Platform.Core.Abstractions;

/// <summary>
/// 进程内缓存 JWT 校验阶段使用的「用户是否启用」结果，降低每请求数据库压力；变更启用状态时必须调用 <see cref="Invalidate"/>。
/// </summary>
public interface IJwtUserEnabledValidationCache
{
    bool TryGet(Guid userId, string tenantId, out bool enabled);

    void Set(Guid userId, string tenantId, bool enabled, TimeSpan ttl);

    void Invalidate(Guid userId, string tenantId);
}
