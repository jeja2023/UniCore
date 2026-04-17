using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;

namespace Platform.AuditLog.Services;

public sealed class AuditLogService(AppDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            AuditEventId = Guid.NewGuid(),
            TenantId = tenantId,
            EventCode = auditEvent.EventCode,
            Description = auditEvent.Description,
            Actor = auditEvent.Actor,
            OccurredAt = auditEvent.OccurredAt,
            Level = auditEvent.Level,
            RequestPath = auditEvent.RequestPath,
            HttpMethod = auditEvent.HttpMethod,
            StatusCode = auditEvent.StatusCode,
            TraceId = auditEvent.TraceId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditEvent>> QueryAsync(
        AuditQueryFilter? filter = null,
        CancellationToken cancellationToken = default) =>
        (await QueryPagedAsync(filter, cancellationToken)).Items;

    public async Task<AuditQueryResult> QueryPagedAsync(
        AuditQueryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var query = dbContext.AuditEvents
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);
        if (filter is not null)
        {
            if (filter.From.HasValue)
            {
                query = query.Where(x => x.OccurredAt >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(x => x.OccurredAt <= filter.To.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.RequestPath))
            {
                var requestPath = filter.RequestPath.Trim();
                query = query.Where(x => x.RequestPath != null && x.RequestPath.Contains(requestPath));
            }

            if (!string.IsNullOrWhiteSpace(filter.HttpMethod))
            {
                var method = filter.HttpMethod.Trim().ToUpperInvariant();
                query = query.Where(x => x.HttpMethod != null && x.HttpMethod.ToUpper() == method);
            }

            if (filter.StatusCode.HasValue)
            {
                query = query.Where(x => x.StatusCode == filter.StatusCode.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Actor))
            {
                var actor = filter.Actor.Trim();
                query = query.Where(x => x.Actor.Contains(actor));
            }

            if (!string.IsNullOrWhiteSpace(filter.EventCode))
            {
                var eventCode = filter.EventCode.Trim();
                query = query.Where(x => x.EventCode.Contains(eventCode));
            }

            if (!string.IsNullOrWhiteSpace(filter.Level))
            {
                var level = filter.Level.Trim();
                query = query.Where(x => x.Level == level);
            }

            if (!string.IsNullOrWhiteSpace(filter.TraceId))
            {
                var traceId = filter.TraceId.Trim();
                query = query.Where(x => x.TraceId != null && x.TraceId.Contains(traceId));
            }
        }

        var sorted = ApplySort(query, filter?.Sort);
        var pageSize = filter?.PageSize is > 0 and <= 200 ? filter.PageSize.Value : 50;
        var page = filter?.Page is > 0 ? filter.Page.Value : 1;
        var limit = filter?.Limit is > 0 and <= 1000 ? filter.Limit.Value : pageSize;
        var size = Math.Min(pageSize, limit);
        var offset = (page - 1) * pageSize;
        var total = await sorted.CountAsync(cancellationToken);

        if (offset > 0)
        {
            sorted = sorted.Skip(offset);
        }

        var items = await sorted
            .Take(size)
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

        return new AuditQueryResult(items, total, page, pageSize);
    }

    private static IQueryable<AuditEventEntity> ApplySort(IQueryable<AuditEventEntity> query, string? rawSort)
    {
        var sortTokens = ParseSort(rawSort);
        IOrderedQueryable<AuditEventEntity>? ordered = null;
        foreach (var token in sortTokens)
        {
            ordered = token switch
            {
                { Field: "occurredat", Desc: true } => ordered is null ? query.OrderByDescending(x => x.OccurredAt) : ordered.ThenByDescending(x => x.OccurredAt),
                { Field: "occurredat", Desc: false } => ordered is null ? query.OrderBy(x => x.OccurredAt) : ordered.ThenBy(x => x.OccurredAt),
                { Field: "eventcode", Desc: true } => ordered is null ? query.OrderByDescending(x => x.EventCode) : ordered.ThenByDescending(x => x.EventCode),
                { Field: "eventcode", Desc: false } => ordered is null ? query.OrderBy(x => x.EventCode) : ordered.ThenBy(x => x.EventCode),
                { Field: "level", Desc: true } => ordered is null ? query.OrderByDescending(x => x.Level) : ordered.ThenByDescending(x => x.Level),
                { Field: "level", Desc: false } => ordered is null ? query.OrderBy(x => x.Level) : ordered.ThenBy(x => x.Level),
                { Field: "actor", Desc: true } => ordered is null ? query.OrderByDescending(x => x.Actor) : ordered.ThenByDescending(x => x.Actor),
                { Field: "actor", Desc: false } => ordered is null ? query.OrderBy(x => x.Actor) : ordered.ThenBy(x => x.Actor),
                { Field: "statuscode", Desc: true } => ordered is null ? query.OrderByDescending(x => x.StatusCode) : ordered.ThenByDescending(x => x.StatusCode),
                { Field: "statuscode", Desc: false } => ordered is null ? query.OrderBy(x => x.StatusCode) : ordered.ThenBy(x => x.StatusCode),
                _ => ordered
            };
        }

        return ordered ?? query.OrderByDescending(x => x.OccurredAt);
    }

    private static IReadOnlyCollection<AuditSortItem> ParseSort(string? rawSort)
    {
        if (string.IsNullOrWhiteSpace(rawSort))
        {
            return new[] { new AuditSortItem("occurredat", true) };
        }

        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "occurredAt",
            "eventCode",
            "level",
            "actor",
            "statusCode"
        };

        var result = rawSort
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token =>
            {
                var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0 || !supported.Contains(parts[0]))
                {
                    return null;
                }

                var desc = parts.Length > 1 ? !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase) : true;
                return new AuditSortItem(parts[0].ToLowerInvariant(), desc);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        return result.Length == 0
            ? new[] { new AuditSortItem("occurredat", true) }
            : result;
    }
}

public sealed record AuditQueryFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? RequestPath = null,
    string? HttpMethod = null,
    int? StatusCode = null,
    string? Actor = null,
    string? EventCode = null,
    string? Level = null,
    string? TraceId = null,
    int? Limit = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

public sealed record AuditEvent(
    string EventCode,
    string Description,
    string Actor,
    DateTimeOffset OccurredAt,
    string Level,
    string? RequestPath = null,
    string? HttpMethod = null,
    int? StatusCode = null,
    string? TraceId = null);

public sealed record AuditQueryResult(
    IReadOnlyCollection<AuditEvent> Items,
    int Total,
    int Page,
    int PageSize);

internal sealed record AuditSortItem(string Field, bool Desc);
