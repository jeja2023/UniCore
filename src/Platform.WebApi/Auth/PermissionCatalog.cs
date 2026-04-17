using Microsoft.AspNetCore.Authorization;

namespace Platform.WebApi.Auth;

public static class PermissionCodes
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

    internal static void RegisterAuthorizationPolicies(AuthorizationOptions options)
    {
        foreach (var binding in PolicyBindings)
        {
            options.AddPolicy(binding.PolicyName, policy => policy.Requirements.Add(new PermissionRequirement(binding.PermissionCode)));
        }
    }

    internal static IReadOnlyCollection<PlatformMenuBinding> GetPlatformMenus() => PlatformMenus;
}
