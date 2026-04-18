using Platform.Core.Abstractions;

namespace Platform.Permission.Services;

public static class PermissionVersionEvents
{
    public const string Changed = "permission.version.changed";
}

public sealed record PermissionVersionChangedPayload(
    string TenantId,
    string Version,
    string Reason);

