using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text.Json;

namespace Platform.Infrastructure.Services;

public sealed class EntityChangeAuditService(AppDbContext dbContext, ITenantContextAccessor tenantContextAccessor, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RecordAsync(
        string entityName,
        string entityId,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var actor = httpContextAccessor.HttpContext?.User?.Identity?.Name
                    ?? httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? "system";

        dbContext.AuditEntityChanges.Add(new AuditEntityChangeEntity
        {
            AuditEntityChangeId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            EntityName = entityName,
            EntityId = entityId,
            Actor = actor,
            OccurredAt = DateTimeOffset.UtcNow,
            ChangesJson = JsonSerializer.Serialize(changes, JsonOptions)
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditEntityChangeDto>> QueryAsync(
        string entityName,
        string entityId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var items = await dbContext.AuditEntityChanges
            .Where(x => x.TenantId == tenantId && x.EntityName == entityName && x.EntityId == entityId)
            .OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        return items.Select(x => new AuditEntityChangeDto(
            x.AuditEntityChangeId,
            x.EntityName,
            x.EntityId,
            x.Actor,
            x.OccurredAt,
            x.ChangesJson)).ToList();
    }
}

public sealed record AuditEntityChangeDto(
    Guid AuditEntityChangeId,
    string EntityName,
    string EntityId,
    string Actor,
    DateTimeOffset OccurredAt,
    string ChangesJson);
