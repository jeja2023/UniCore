namespace Platform.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text.Json;

public sealed class JobSchedulingOptions
{
    public const string Section = "JobScheduling";
    public bool Enabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(20);
}

public sealed class JobSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<JobSchedulingOptions> options,
    ILogger<JobSchedulerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Job scheduler disabled.");
            return;
        }

        using var timer = new PeriodicTimer(options.Value.PollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExecuteBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Execute scheduled jobs failed.");
            }
        }
    }

    private async Task ExecuteBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var candidateJobs = await dbContext.ScheduledJobs
            .AsNoTracking()
            .Where(x => x.Status == "Pending" && x.RunAt <= DateTimeOffset.UtcNow)
            .OrderBy(x => x.RunAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (candidateJobs.Count == 0)
        {
            return;
        }

        var dueJobs = new List<ScheduledJobEntity>(candidateJobs.Count);
        foreach (var candidate in candidateJobs)
        {
            int claimed;
            try
            {
                claimed = await dbContext.ScheduledJobs
                    .Where(x => x.ScheduledJobId == candidate.ScheduledJobId && x.Status == "Pending")
                    .ExecuteUpdateAsync(
                        updates => updates.SetProperty(x => x.Status, "Running"),
                        cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // InMemory provider used by integration tests does not support ExecuteUpdateAsync.
                var row = await dbContext.ScheduledJobs
                    .FirstOrDefaultAsync(x => x.ScheduledJobId == candidate.ScheduledJobId, cancellationToken);
                if (row is null || row.Status != "Pending")
                {
                    claimed = 0;
                }
                else
                {
                    row.Status = "Running";
                    await dbContext.SaveChangesAsync(cancellationToken);
                    claimed = 1;
                }
            }
            if (claimed != 1)
            {
                continue;
            }

            var runningJob = await dbContext.ScheduledJobs
                .FirstOrDefaultAsync(x => x.ScheduledJobId == candidate.ScheduledJobId, cancellationToken);
            if (runningJob is not null)
            {
                dueJobs.Add(runningJob);
            }
        }

        if (dueJobs.Count == 0)
        {
            return;
        }

        var messageIds = dueJobs
            .Select(TryParseNotificationMessageId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var messages = messageIds.Length == 0
            ? new Dictionary<Guid, NotificationMessageEntity>()
            : await dbContext.NotificationMessages
                .Where(x => messageIds.Contains(x.NotificationMessageId))
                .ToDictionaryAsync(x => x.NotificationMessageId, cancellationToken);

        foreach (var job in dueJobs)
        {
            try
            {
                await ExecuteNotificationRetryAsync(job, messages, notificationService, cancellationToken);

                job.Status = "Completed";
                job.FinishedAt = DateTimeOffset.UtcNow;
                job.Error = null;
            }
            catch (Exception ex)
            {
                job.RetryCount += 1;
                job.Error = ex.Message;
                job.Status = job.RetryCount > job.MaxRetries ? "Failed" : "Pending";
                if (job.Status == "Pending")
                {
                    job.RunAt = DateTimeOffset.UtcNow.AddMinutes(1);
                }
                else
                {
                    job.FinishedAt = DateTimeOffset.UtcNow;
                }
            }

        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ExecuteNotificationRetryAsync(
        ScheduledJobEntity job,
        IReadOnlyDictionary<Guid, NotificationMessageEntity> messages,
        NotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var notificationMessageId = TryParseNotificationMessageId(job)
            ?? throw new InvalidOperationException($"调度任务 payload 无法解析通知消息标识: {job.ScheduledJobId}");
        if (!messages.TryGetValue(notificationMessageId, out var message))
        {
            throw new InvalidOperationException($"调度任务对应的通知消息不存在: {notificationMessageId}");
        }

        if (string.Equals(job.JobType, "notification.webhook.retry", StringComparison.OrdinalIgnoreCase))
        {
            await notificationService.TrySendWebhookAsync(message, cancellationToken);
            return;
        }

        if (string.Equals(job.JobType, "notification.email.retry", StringComparison.OrdinalIgnoreCase))
        {
            await notificationService.TrySendEmailAsync(message, cancellationToken);
            return;
        }

        if (string.Equals(job.JobType, "notification.sms.retry", StringComparison.OrdinalIgnoreCase))
        {
            await notificationService.TrySendSmsAsync(message, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"不支持的调度任务类型: {job.JobType}");
    }

    private static Guid? TryParseNotificationMessageId(ScheduledJobEntity job)
    {
        if (!job.JobType.StartsWith("notification.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<WebhookRetryPayload>(job.Payload);
        if (payload is null || !Guid.TryParse(payload.NotificationMessageId, out var id))
        {
            return null;
        }

        return id;
    }
}

public sealed class JobSchedulingService(AppDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
{
    public async Task<ScheduledJobEntity> EnqueueWebhookRetryAsync(Guid notificationMessageId, DateTimeOffset runAt, CancellationToken cancellationToken = default)
    {
        var job = new ScheduledJobEntity
        {
            ScheduledJobId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            JobType = "notification.webhook.retry",
            Payload = JsonSerializer.Serialize(new WebhookRetryPayload(notificationMessageId.ToString())),
            RunAt = runAt,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            MaxRetries = 3
        };
        dbContext.ScheduledJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<ScheduledJobEntity> EnqueueEmailRetryAsync(Guid notificationMessageId, DateTimeOffset runAt, CancellationToken cancellationToken = default)
    {
        var job = new ScheduledJobEntity
        {
            ScheduledJobId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            JobType = "notification.email.retry",
            Payload = JsonSerializer.Serialize(new WebhookRetryPayload(notificationMessageId.ToString())),
            RunAt = runAt,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            MaxRetries = 3
        };
        dbContext.ScheduledJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<ScheduledJobEntity> EnqueueSmsRetryAsync(Guid notificationMessageId, DateTimeOffset runAt, CancellationToken cancellationToken = default)
    {
        var job = new ScheduledJobEntity
        {
            ScheduledJobId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            JobType = "notification.sms.retry",
            Payload = JsonSerializer.Serialize(new WebhookRetryPayload(notificationMessageId.ToString())),
            RunAt = runAt,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            MaxRetries = 3
        };
        dbContext.ScheduledJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<IReadOnlyCollection<ScheduledJobEntity>> GetJobsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ScheduledJobs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantContextAccessor.TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
}

public sealed record WebhookRetryPayload(string NotificationMessageId);
