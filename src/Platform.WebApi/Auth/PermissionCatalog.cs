using Microsoft.AspNetCore.Authorization;
using Platform.Core.Security;

namespace Platform.WebApi.Auth;

/// <summary>与 <see cref="PlatformPermissionCodes"/> 对齐的别名，便于 WebApi 代码引用。</summary>
public static class PermissionCodes
{
    public const string UserRead = PlatformPermissionCodes.UserRead;
    public const string UserCreate = PlatformPermissionCodes.UserCreate;
    public const string UserUpdate = PlatformPermissionCodes.UserUpdate;
    public const string PermissionRead = PlatformPermissionCodes.PermissionRead;
    public const string PermissionUpdate = PlatformPermissionCodes.PermissionUpdate;
    public const string AuditRead = PlatformPermissionCodes.AuditRead;
    public const string PlatformFeatureRead = PlatformPermissionCodes.PlatformFeatureRead;
    public const string PlatformCacheRead = PlatformPermissionCodes.PlatformCacheRead;
    public const string PlatformCacheWrite = PlatformPermissionCodes.PlatformCacheWrite;
    public const string PlatformFileRead = PlatformPermissionCodes.PlatformFileRead;
    public const string PlatformFileWrite = PlatformPermissionCodes.PlatformFileWrite;
}

internal sealed record PermissionPolicyBinding(string PolicyName, string PermissionCode);
internal sealed record PlatformMenuBinding(string Key, string Title, string Path, string? PermissionCode);

public static class PermissionCatalog
{
    private static readonly PermissionPolicyBinding[] PolicyBindings =
    [
        new(PermissionPolicies.UserRead, PermissionCodes.UserRead),
        new(PermissionPolicies.UserCreate, PermissionCodes.UserCreate),
        new(PermissionPolicies.UserUpdate, PermissionCodes.UserUpdate),
        new(PermissionPolicies.PermissionRead, PermissionCodes.PermissionRead),
        new(PermissionPolicies.PermissionUpdate, PermissionCodes.PermissionUpdate),
        new(PermissionPolicies.AuditRead, PermissionCodes.AuditRead),
        new(PermissionPolicies.PlatformFeatureRead, PermissionCodes.PlatformFeatureRead),
        new(PermissionPolicies.PlatformCacheRead, PermissionCodes.PlatformCacheRead),
        new(PermissionPolicies.PlatformCacheWrite, PermissionCodes.PlatformCacheWrite),
        new(PermissionPolicies.PlatformFileRead, PermissionCodes.PlatformFileRead),
        new(PermissionPolicies.PlatformFileWrite, PermissionCodes.PlatformFileWrite)
    ];

    private static readonly PlatformMenuBinding[] PlatformMenus =
    [
        new("platform.home", "首页", "/", null),
        new("platform.users", "用户", "/identity/users", PermissionCodes.UserRead),
        new("platform.data-scope", "数据权限治理", "/permission/data-scope", PermissionCodes.PermissionRead),
        new("platform.modules", "模块", "/modules", null)
    ];

    internal static PermissionPolicyBinding[] GetPolicyBindings() => PolicyBindings;

    internal static void RegisterAuthorizationPolicies(AuthorizationOptions options)
    {
        foreach (var binding in PolicyBindings)
        {
            options.AddPolicy(binding.PolicyName, policy => policy.Requirements.Add(new PermissionRequirement(binding.PermissionCode)));
        }
    }

    internal static IReadOnlyCollection<PlatformMenuBinding> GetPlatformMenus() => PlatformMenus;
}
