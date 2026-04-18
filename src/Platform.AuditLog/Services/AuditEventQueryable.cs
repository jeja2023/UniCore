using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence.Entities;

namespace Platform.AuditLog.Services;

internal static class AuditEventQueryable
{
    public static bool IsPostgreSql(this DbContext db) =>
        string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);

    /// <summary>
    /// 在 PostgreSQL 上使用 ILIKE 子串匹配（可由 pg_trgm + GIN 索引加速）；其他提供程序回退到 Contains。
    /// </summary>
    public static IQueryable<AuditEventEntity> ApplyAuditListFilters(
        this IQueryable<AuditEventEntity> query,
        AuditQueryFilter? filter,
        bool useIlikeSubstring)
    {
        if (filter is null)
        {
            return query;
        }

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
            query = useIlikeSubstring
                ? query.Where(x => x.RequestPath != null && EF.Functions.ILike(x.RequestPath, LikeContainsPattern(requestPath)))
                : query.Where(x => x.RequestPath != null && x.RequestPath.Contains(requestPath));
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
            query = useIlikeSubstring
                ? query.Where(x => EF.Functions.ILike(x.Actor, LikeContainsPattern(actor)))
                : query.Where(x => x.Actor.Contains(actor));
        }

        if (!string.IsNullOrWhiteSpace(filter.EventCode))
        {
            var eventCode = filter.EventCode.Trim();
            query = useIlikeSubstring
                ? query.Where(x => EF.Functions.ILike(x.EventCode, LikeContainsPattern(eventCode)))
                : query.Where(x => x.EventCode.Contains(eventCode));
        }

        if (!string.IsNullOrWhiteSpace(filter.Level))
        {
            var level = filter.Level.Trim();
            query = query.Where(x => x.Level == level);
        }

        if (!string.IsNullOrWhiteSpace(filter.TraceId))
        {
            var traceId = filter.TraceId.Trim();
            query = useIlikeSubstring
                ? query.Where(x => x.TraceId != null && EF.Functions.ILike(x.TraceId, LikeContainsPattern(traceId)))
                : query.Where(x => x.TraceId != null && x.TraceId.Contains(traceId));
        }

        return query;
    }

    /// <summary>构造 ILIKE 子串模式，并对 LIKE 特殊字符做转义（PostgreSQL 默认转义符为 \）。</summary>
    private static string LikeContainsPattern(string literal)
    {
        var escaped = literal
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return "%" + escaped + "%";
    }
}
