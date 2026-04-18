using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Platform.AuditLog.Metrics;

namespace Platform.AuditLog.Services;

public sealed class AuditExportService(
    AppDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<AuditExportCallbackOptions> callbackOptions,
    AuditExportMetricsStore metricsStore,
    ILogger<AuditExportService> logger)
{
    private static readonly JsonSerializerOptions CallbackJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuditExportJobInfo> CreateJobAsync(
        string createdBy,
        AuditQueryFilter filter,
        IReadOnlyCollection<string> fields,
        Uri? callbackUrl = null,
        string? downloadUrl = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tenantId = tenantContextAccessor.TenantId;
        var job = new AuditExportJobEntity
        {
            JobId = id,
            TenantId = tenantId,
            CreatedBy = createdBy,
            Status = "Pending",
            CreatedAt = now,
            Completed = false,
            RetryCount = 0,
            MaxRetries = 5,
            NextAttemptAt = now,
            DeadLettered = false,
            CsvContent = JsonSerializer.Serialize(new AuditExportJobPayload(filter, fields, callbackUrl?.ToString(), downloadUrl), CallbackJsonOptions)
        };
        dbContext.AuditExportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToInfo(job);
    }

    public async Task<AuditExportJobInfo?> GetJobAsync(Guid jobId, string requester, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var job = await dbContext.AuditExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId && x.TenantId == tenantId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        EnsureOwner(job, requester);
        return ToInfo(job);
    }

    public async Task<string?> GetCompletedCsvAsync(Guid jobId, string requester, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var job = await dbContext.AuditExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId && x.TenantId == tenantId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        EnsureOwner(job, requester);
        return job is { Completed: true } ? job.CsvContent : null;
    }

    public async Task<AuditExportJobPageResult> QueryJobsAsync(
        string requester,
        AuditExportJobQueryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var page = filter?.Page is > 0 ? filter.Page.Value : 1;
        var pageSize = filter?.PageSize is > 0 and <= 100 ? filter.PageSize.Value : 20;
        var tenantId = tenantContextAccessor.TenantId;
        var query = dbContext.AuditExportJobs
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.TenantId == tenantId && x.CreatedBy == requester);
        if (!string.IsNullOrWhiteSpace(filter?.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (filter?.JobId.HasValue == true)
        {
            query = query.Where(x => x.JobId == filter.JobId.Value);
        }

        if (filter?.From.HasValue == true)
        {
            query = query.Where(x => x.CreatedAt >= filter.From.Value);
        }

        if (filter?.To.HasValue == true)
        {
            query = query.Where(x => x.CreatedAt <= filter.To.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var sortedQuery = ApplySorting(query, filter);
        var items = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToInfo(x))
            .ToListAsync(cancellationToken);

        return new AuditExportJobPageResult(items, total, page, pageSize);
    }

    public async Task<AuditExportJobPageResult> QueryDeadLetterJobsAsync(
        string requester,
        AuditExportJobQueryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var page = filter?.Page is > 0 ? filter.Page.Value : 1;
        var pageSize = filter?.PageSize is > 0 and <= 100 ? filter.PageSize.Value : 20;
        var tenantId = tenantContextAccessor.TenantId;
        var query = dbContext.AuditExportJobs
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.TenantId == tenantId && x.CreatedBy == requester && x.DeadLettered);

        if (filter?.JobId.HasValue == true)
        {
            query = query.Where(x => x.JobId == filter.JobId.Value);
        }

        if (filter?.From.HasValue == true)
        {
            query = query.Where(x => x.CreatedAt >= filter.From.Value);
        }

        if (filter?.To.HasValue == true)
        {
            query = query.Where(x => x.CreatedAt <= filter.To.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var sortedQuery = ApplySorting(query, filter);
        var items = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToInfo(x))
            .ToListAsync(cancellationToken);

        return new AuditExportJobPageResult(items, total, page, pageSize);
    }

    public async Task<AuditExportJobInfo?> ReplayDeadLetterJobAsync(Guid jobId, string requester, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var job = await dbContext.AuditExportJobs
            .FirstOrDefaultAsync(x => x.JobId == jobId && x.TenantId == tenantId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        EnsureOwner(job, requester);
        if (!job.DeadLettered)
        {
            throw new Platform.Core.Common.AppException(
                Platform.Core.Common.ErrorCodes.ValidationError,
                "仅死信任务可重放。");
        }

        job.DeadLettered = false;
        job.Status = "Retry";
        job.Completed = false;
        job.CompletedAt = null;
        job.Error = null;
        job.RetryCount = 0;
        job.NextAttemptAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        metricsStore.Increment("dlq_replayed");
        return ToInfo(job);
    }

    public async Task<AuditExportJobInfo?> DiscardDeadLetterJobAsync(Guid jobId, string requester, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var job = await dbContext.AuditExportJobs
            .FirstOrDefaultAsync(x => x.JobId == jobId && x.TenantId == tenantId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        EnsureOwner(job, requester);
        if (!job.DeadLettered)
        {
            throw new Platform.Core.Common.AppException(
                Platform.Core.Common.ErrorCodes.ValidationError,
                "仅死信任务可丢弃。");
        }

        job.Status = "Discarded";
        job.Completed = true;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        metricsStore.Increment("dlq_discarded");
        return ToInfo(job);
    }

    public async Task<int> CleanupExpiredJobsAsync(TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow.Subtract(ttl);
        var expiredJobs = await dbContext.AuditExportJobs
            .Where(x => x.CreatedAt < threshold)
            .ToListAsync(cancellationToken);
        if (expiredJobs.Count == 0)
        {
            return 0;
        }

        dbContext.AuditExportJobs.RemoveRange(expiredJobs);
        await dbContext.SaveChangesAsync(cancellationToken);
        return expiredJobs.Count;
    }

    internal async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var job = await dbContext.AuditExportJobs
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(
                x => (x.Status == "Pending" || x.Status == "Retry") &&
                     !x.Completed &&
                     !x.DeadLettered &&
                     (x.NextAttemptAt == null || x.NextAttemptAt <= now),
                cancellationToken);
        if (job is null)
        {
            return false;
        }

        var payload = ParseJobPayload(job);
        if (payload is null)
        {
            job.Status = "Failed";
            job.Completed = true;
            job.CompletedAt = now;
            job.Error = "导出任务参数损坏，无法解析。";
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        job.Status = "Processing";
        job.Error = null;
        job.LastAttemptAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var sw = Stopwatch.StartNew();
        try
        {
            var callbackUri = string.IsNullOrWhiteSpace(payload.CallbackUrl) ? null : new Uri(payload.CallbackUrl);
            var rows = await QueryAuditRowsAsync(job.TenantId, payload.Filter, cancellationToken);
            var csv = AuditCsvBuilder.Build(rows, payload.Fields);
            job.Status = "Completed";
            job.Completed = true;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.CsvContent = csv;
            job.Error = null;
            metricsStore.Record("success", sw.Elapsed);
        }
        catch (Exception ex)
        {
            job.RetryCount += 1;
            job.Error = ex.Message;

            if (job.RetryCount >= job.MaxRetries)
            {
                job.Status = "DeadLettered";
                job.DeadLettered = true;
                job.Completed = true;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.NextAttemptAt = null;
                metricsStore.Record("deadlettered", sw.Elapsed);
            }
            else
            {
                job.Status = "Retry";
                job.Completed = false;
                job.CompletedAt = null;
                job.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(ComputeBackoffSeconds(job.RetryCount));
                metricsStore.Record("retry", sw.Elapsed);
            }
        }
        finally
        {
            sw.Stop();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(payload.CallbackUrl) && job.Completed)
        {
            await PushCallbackAsync(new Uri(payload.CallbackUrl), job, payload.DownloadUrl, cancellationToken);
        }

        return true;
    }

    private static int ComputeBackoffSeconds(int retryCount)
    {
        // 1, 2, 4, 8, 16, 30... capped
        var exp = Math.Min(retryCount - 1, 5);
        var seconds = (int)Math.Pow(2, exp);
        return Math.Min(seconds, 30);
    }

    internal async Task<int> MarkStaleProcessingJobsAsFailedAsync(CancellationToken cancellationToken)
    {
        var staleJobs = await dbContext.AuditExportJobs
            .Where(x => x.Status == "Processing" && !x.Completed)
            .ToListAsync(cancellationToken);
        if (staleJobs.Count == 0)
        {
            return 0;
        }

        foreach (var job in staleJobs)
        {
            job.Status = "Retry";
            job.Completed = false;
            job.CompletedAt = null;
            job.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(2);
            job.Error ??= "任务在服务重启前中断，已进入重试队列。";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return staleJobs.Count;
    }

    private async Task PushCallbackAsync(Uri callbackUrl, AuditExportJobEntity job, string? downloadUrl, CancellationToken cancellationToken)
    {
        try
        {
            var resolvedDownloadUrl = string.IsNullOrWhiteSpace(downloadUrl)
                ? null
                : downloadUrl.Replace("{jobId}", job.JobId.ToString("D"), StringComparison.Ordinal);
            var payload = new AuditExportJobCallbackPayload(
                job.JobId,
                job.Status,
                job.CreatedBy,
                job.CreatedAt,
                job.CompletedAt,
                job.Error,
                resolvedDownloadUrl);
            var body = JsonSerializer.Serialize(payload, CallbackJsonOptions);
            var request = BuildCallbackRequest(callbackUrl, body);
            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Audit export callback failed. JobId={JobId}, Callback={Callback}, StatusCode={StatusCode}",
                    job.JobId,
                    callbackUrl,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit export callback threw exception. JobId={JobId}, Callback={Callback}", job.JobId, callbackUrl);
        }
    }

    private HttpRequestMessage BuildCallbackRequest(Uri callbackUrl, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-UniCore-Event", "audit-export.completed");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        request.Headers.Add("X-UniCore-Timestamp", timestamp);
        request.Headers.Add("X-UniCore-Nonce", nonce);

        var signingKey = callbackOptions.Value.SigningKey;
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            var signature = ComputeSignature(signingKey, timestamp, nonce, body);
            request.Headers.Add("X-UniCore-Signature", signature);
        }

        return request;
    }

    private static string ComputeSignature(string signingKey, string timestamp, string nonce, string body)
    {
        var payloadToSign = $"{timestamp}.{nonce}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToSign));
        return Convert.ToHexString(hash);
    }

    private static AuditExportJobInfo ToInfo(AuditExportJobEntity entity) =>
        new(
            entity.JobId,
            entity.CreatedBy,
            entity.Status,
            entity.CreatedAt,
            entity.CompletedAt,
            entity.Error,
            entity.RetryCount,
            entity.MaxRetries,
            entity.NextAttemptAt,
            entity.LastAttemptAt,
            entity.DeadLettered);

    private static AuditExportJobPayload? ParseJobPayload(AuditExportJobEntity job)
    {
        if (string.IsNullOrWhiteSpace(job.CsvContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AuditExportJobPayload>(job.CsvContent, CallbackJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureOwner(AuditExportJobEntity entity, string requester)
    {
        if (!entity.CreatedBy.Equals(requester, StringComparison.Ordinal))
        {
            throw new Platform.Core.Common.AppException(
                Platform.Core.Common.ErrorCodes.Forbidden,
                "仅任务创建者可访问导出任务。",
                403);
        }
    }

    private static IQueryable<AuditExportJobEntity> ApplySorting(IQueryable<AuditExportJobEntity> query, AuditExportJobQueryFilter? filter)
    {
        var sortBy = filter?.SortBy?.Trim().ToLowerInvariant();
        var sortDir = filter?.SortDir?.Trim().ToLowerInvariant() == "asc" ? "asc" : "desc";
        var asc = sortDir == "asc";
        return sortBy switch
        {
            "status" => asc
                ? query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.Status).ThenByDescending(x => x.CreatedAt),
            "completedat" => asc
                ? query.OrderBy(x => x.CompletedAt).ThenByDescending(x => x.CreatedAt)
                : query.OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.CreatedAt),
            "createdat" or _ => asc
                ? query.OrderBy(x => x.CreatedAt)
                : query.OrderByDescending(x => x.CreatedAt),
        };
    }

    private async Task<IReadOnlyCollection<AuditEvent>> QueryAuditRowsAsync(string tenantId, AuditQueryFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEvents
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.TenantId == tenantId)
            .ApplyAuditListFilters(filter, dbContext.IsPostgreSql());

        var rows = await query
            .OrderByDescending(x => x.OccurredAt)
            .Take(filter.Limit is > 0 and <= 5000 ? filter.Limit.Value : 1000)
            .Select(x => new AuditEvent(
                x.EventCode,
                x.Description,
                x.Actor,
                x.OccurredAt,
                x.Level,
                x.RequestPath,
                x.HttpMethod,
                x.StatusCode,
                x.TraceId))
            .ToListAsync(cancellationToken);

        return rows;
    }
}

public sealed record AuditExportJobPayload(
    AuditQueryFilter Filter,
    IReadOnlyCollection<string> Fields,
    string? CallbackUrl,
    string? DownloadUrl);

public sealed class AuditExportJobProcessorHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditExportJobProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarkStaleJobsOnStartupAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AuditExportService>();
                var processed = await service.ProcessNextPendingJobAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Process audit export job failed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task MarkStaleJobsOnStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AuditExportService>();
            var affected = await service.MarkStaleProcessingJobsAsFailedAsync(cancellationToken);
            if (affected > 0)
            {
                logger.LogWarning("Marked stale audit export jobs as failed on startup. Count={Count}", affected);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark stale audit export jobs on startup.");
        }
    }
}

public static class AuditCsvBuilder
{
    private static readonly string[] DefaultFields =
    [
        "occurredAt", "eventCode", "level", "actor", "httpMethod", "requestPath", "statusCode", "traceId", "description"
    ];

    public static string Build(IReadOnlyCollection<AuditEvent> rows, IReadOnlyCollection<string>? selectedFields)
    {
        var fields = selectedFields is { Count: > 0 } ? selectedFields : DefaultFields;
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(string.Join(",", fields.Select(Csv)));
        foreach (var row in rows)
        {
            var values = fields.Select(field => Csv(GetFieldValue(row, field)));
            builder.AppendLine(string.Join(",", values));
        }

        return builder.ToString();
    }

    public static IReadOnlyCollection<string> ParseFields(string? rawFields)
    {
        if (string.IsNullOrWhiteSpace(rawFields))
        {
            return Array.Empty<string>();
        }

        var allowed = new HashSet<string>(DefaultFields, StringComparer.OrdinalIgnoreCase);
        return rawFields
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => allowed.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetFieldValue(AuditEvent row, string field) =>
        field.ToLowerInvariant() switch
        {
            "occurredat" => row.OccurredAt.ToString("O"),
            "eventcode" => row.EventCode,
            "level" => row.Level,
            "actor" => row.Actor,
            "httpmethod" => row.HttpMethod,
            "requestpath" => row.RequestPath,
            "statuscode" => row.StatusCode?.ToString(),
            "traceid" => row.TraceId,
            "description" => row.Description,
            _ => null
        };

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}

public sealed record AuditExportJobInfo(
    Guid JobId,
    string CreatedBy,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    int RetryCount,
    int MaxRetries,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    bool DeadLettered);
public sealed record AuditExportJobPageResult(IReadOnlyCollection<AuditExportJobInfo> Items, int Total, int Page, int PageSize);
public sealed record AuditExportJobQueryFilter(
    Guid? JobId = null,
    string? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int? Page = null,
    int? PageSize = null,
    string? SortBy = null,
    string? SortDir = null);
public sealed class AuditExportCallbackOptions
{
    public const string Section = "AuditExportCallback";

    public string? SigningKey { get; set; }
}

public sealed record AuditExportJobCallbackPayload(
    Guid JobId,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    string? DownloadUrl);
