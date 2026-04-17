namespace Platform.Core.Security;

/// <summary>
/// 默认租户的初始角色权限（须与 <see cref="PlatformPermissionCodes"/> 一致）。
/// </summary>
public static class PlatformPermissionSeed
{
    /// <summary>管理员角色应拥有的平台权限（完整集合）。</summary>
    public static IReadOnlyList<string> AdminRolePlatformPermissionCodes { get; } =
    [
        PlatformPermissionCodes.UserRead,
        PlatformPermissionCodes.UserCreate,
        PlatformPermissionCodes.UserUpdate,
        PlatformPermissionCodes.PermissionRead,
        PlatformPermissionCodes.PermissionUpdate,
        PlatformPermissionCodes.AuditRead,
        PlatformPermissionCodes.PlatformFeatureRead,
        PlatformPermissionCodes.PlatformCacheRead,
        PlatformPermissionCodes.PlatformCacheWrite,
        PlatformPermissionCodes.PlatformFileRead,
        PlatformPermissionCodes.PlatformFileWrite
    ];

    /// <summary>运维角色默认平台权限。</summary>
    public static IReadOnlyList<string> OpsRolePlatformPermissionCodes { get; } =
    [
        PlatformPermissionCodes.AuditRead
    ];
}
