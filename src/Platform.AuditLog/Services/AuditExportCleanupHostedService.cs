using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Platform.AuditLog.Services;

public sealed class AuditExportCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditExportCleanupOptions> options,
    ILogger<AuditExportCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.Enabled is false)
        {
            logger.LogInformation("Audit export cleanup hosted service is disabled.");
            return;
        }

        await RunCleanupOnceAsync(settings, stoppingToken);

        using var timer = new PeriodicTimer(settings.RunInterval);
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupOnceAsync(settings, stoppingToken);
        }
    }

    private async Task RunCleanupOnceAsync(AuditExportCleanupOptions settings, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuditExportService>();
        var removed = await service.CleanupExpiredJobsAsync(settings.Ttl, cancellationToken);
        if (removed > 0)
        {
            logger.LogInformation("Cleaned up {Count} expired audit export jobs.", removed);
        }
    }
}

public sealed class AuditExportCleanupOptions
{
    public const string Section = "AuditExportCleanup";

    public bool Enabled { get; set; } = true;
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan RunInterval { get; set; } = TimeSpan.FromMinutes(30);
}
