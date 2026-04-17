namespace Platform.Core.Security;

/// <summary>
/// 平台内置权限码（与鉴权策略、种子、平台菜单对齐的唯一来源）。
/// </summary>
public static class PlatformPermissionCodes
{
    public const string UserRead = "user.read";
    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string PermissionRead = "permission.read";
    public const string PermissionUpdate = "permission.update";
    public const string AuditRead = "audit.read";
    public const string PlatformFeatureRead = "platform.feature.read";
    public const string PlatformCacheRead = "platform.cache.read";
    public const string PlatformCacheWrite = "platform.cache.write";
    public const string PlatformFileRead = "platform.file.read";
    public const string PlatformFileWrite = "platform.file.write";
}
