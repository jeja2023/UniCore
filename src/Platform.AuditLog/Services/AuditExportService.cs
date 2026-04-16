using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Platform.AuditLog.Services;

public sealed class AuditExportService(
    AppDbContext dbContext,
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<AuditExportCallbackOptions> callbackOptions,
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
        var job = new AuditExportJobEntity
        {
            JobId = id,
            CreatedBy = createdBy,
            Status = "Processing",
            CreatedAt = now,
            Completed = false
        };
        dbContext.AuditExportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        _ = Task.Run(() => ProcessJobAsync(id, filter, fields, callbackUrl, downloadUrl));
        return ToInfo(job);
    }

    public async Task<AuditExportJobInfo?> GetJobAsync(Guid jobId, string requester, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.AuditExportJobs.FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        EnsureOwner(job, requester);
        return ToInfo(job);
    }

    public async Task<string?> GetCompletedCsvAsync(Guid jobId, string requester, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.AuditExportJobs.FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
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
        var query = dbContext.AuditExportJobs.AsQueryable().Where(x => x.CreatedBy == requester);
        if (!string.IsNullOrWhiteSpace(filter?.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(x => x.Status == status);
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
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToInfo(x))
            .ToListAsync(cancellationToken);

        return new AuditExportJobPageResult(items, total, page, pageSize);
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

    private async Task ProcessJobAsync(
        Guid jobId,
        AuditQueryFilter filter,
        IReadOnlyCollection<string> fields,
        Uri? callbackUrl,
        string? downloadUrl)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditLogService = scope.ServiceProvider.GetRequiredService<AuditLogService>();

        var job = await scopedDb.AuditExportJobs.FirstOrDefaultAsync(x => x.JobId == jobId);
        if (job is null)
        {
            return;
        }

        try
        {
            var rows = await auditLogService.QueryAsync(filter);
            var csv = AuditCsvBuilder.Build(rows, fields);
            job.Status = "Completed";
            job.Completed = true;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.CsvContent = csv;
            job.Error = null;
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.Completed = true;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Error = ex.Message;
        }

        await scopedDb.SaveChangesAsync();

        if (callbackUrl is not null)
        {
            await PushCallbackAsync(callbackUrl, job, downloadUrl);
        }
    }

    private async Task PushCallbackAsync(Uri callbackUrl, AuditExportJobEntity job, string? downloadUrl)
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
            using var response = await client.SendAsync(request);
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
        new(entity.JobId, entity.CreatedBy, entity.Status, entity.CreatedAt, entity.CompletedAt, entity.Error);

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

public sealed record AuditExportJobInfo(Guid JobId, string CreatedBy, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? Error);
public sealed record AuditExportJobPageResult(IReadOnlyCollection<AuditExportJobInfo> Items, int Total, int Page, int PageSize);
public sealed record AuditExportJobQueryFilter(string? Status = null, DateTimeOffset? From = null, DateTimeOffset? To = null, int? Page = null, int? PageSize = null);
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
