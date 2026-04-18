using Platform.Core.Abstractions;

namespace Platform.Permission.Services;

public sealed class PermissionVersionService(IAppCache appCache, IAppEventBus eventBus)
{
    private const string PermissionVersionPrefix = "permission:version:";

    public async Task<string> BumpAsync(string tenantId, string reason, string? traceId = null, CancellationToken cancellationToken = default)
    {
        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        await appCache.SetAsync($"{PermissionVersionPrefix}{tenantId}", version, TimeSpan.FromDays(7), cancellationToken);

        await eventBus.PublishAsync(
            new AppEvent<PermissionVersionChangedPayload>(
                PermissionVersionEvents.Changed,
                new PermissionVersionChangedPayload(tenantId, version, reason),
                DateTimeOffset.UtcNow,
                traceId),
            cancellationToken);

        return version;
    }
}

