using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Common;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;

namespace Platform.Infrastructure.Services;

public sealed class DictionaryService(AppDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
{
    public async Task<IReadOnlyCollection<string>> GetDictionariesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        return await dbContext.DictionaryItems
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.DictionaryCode)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DictionaryItemDto>> GetItemsAsync(string dictionaryCode, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var code = NormalizeCode(dictionaryCode);
        return await dbContext.DictionaryItems
            .Where(x => x.TenantId == tenantId && x.DictionaryCode == code)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.ItemCode)
            .Select(x => new DictionaryItemDto(x.DictionaryItemId, x.DictionaryCode, x.ItemCode, x.ItemName, x.Sort, x.Enabled, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DictionaryItemDto> UpsertItemAsync(
        string dictionaryCode,
        string itemCode,
        string itemName,
        int sort,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var code = NormalizeCode(dictionaryCode);
        var normalizedItemCode = NormalizeItemCode(itemCode);
        var normalizedName = NormalizeName(itemName);

        var entity = await dbContext.DictionaryItems.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.DictionaryCode == code && x.ItemCode == normalizedItemCode,
            cancellationToken);

        if (entity is null)
        {
            entity = new DictionaryItemEntity
            {
                DictionaryItemId = Guid.NewGuid(),
                TenantId = tenantId,
                DictionaryCode = code,
                ItemCode = normalizedItemCode,
                ItemName = normalizedName,
                Sort = sort,
                Enabled = enabled,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.DictionaryItems.Add(entity);
        }
        else
        {
            entity.ItemName = normalizedName;
            entity.Sort = sort;
            entity.Enabled = enabled;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new DictionaryItemDto(entity.DictionaryItemId, entity.DictionaryCode, entity.ItemCode, entity.ItemName, entity.Sort, entity.Enabled, entity.UpdatedAt);
    }

    public async Task DeleteItemAsync(string dictionaryCode, string itemCode, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.TenantId;
        var code = NormalizeCode(dictionaryCode);
        var normalizedItemCode = NormalizeItemCode(itemCode);

        var entity = await dbContext.DictionaryItems.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.DictionaryCode == code && x.ItemCode == normalizedItemCode,
            cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.DictionaryItems.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeCode(string dictionaryCode)
    {
        var code = (dictionaryCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AppException(ErrorCodes.ValidationError, "dictionaryCode 不能为空。");
        }
        if (code.Length > 128)
        {
            throw new AppException(ErrorCodes.ValidationError, "dictionaryCode 长度不能超过 128。");
        }
        return code;
    }

    private static string NormalizeItemCode(string itemCode)
    {
        var code = (itemCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AppException(ErrorCodes.ValidationError, "itemCode 不能为空。");
        }
        if (code.Length > 128)
        {
            throw new AppException(ErrorCodes.ValidationError, "itemCode 长度不能超过 128。");
        }
        return code;
    }

    private static string NormalizeName(string name)
    {
        var value = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AppException(ErrorCodes.ValidationError, "itemName 不能为空。");
        }
        if (value.Length > 256)
        {
            throw new AppException(ErrorCodes.ValidationError, "itemName 长度不能超过 256。");
        }
        return value;
    }
}

public sealed record DictionaryItemDto(
    Guid DictionaryItemId,
    string DictionaryCode,
    string ItemCode,
    string ItemName,
    int Sort,
    bool Enabled,
    DateTimeOffset UpdatedAt);
