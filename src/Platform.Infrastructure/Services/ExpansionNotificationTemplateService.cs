namespace Platform.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Platform.Core.Common;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class NotificationTemplateService(
    AppDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor)
{
    public async Task<NotificationTemplateDetail> PublishVersionAsync(
        string templateCode,
        string title,
        string content,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(templateCode);
        var normalizedTitle = NormalizeTitle(title);
        var normalizedContent = NormalizeContent(content);
        var actor = NormalizeActor(changedBy);
        var tenantId = tenantContextAccessor.TenantId;

        var template = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == normalizedCode,
            cancellationToken);
        if (template is null)
        {
            template = new NotificationTemplateEntity
            {
                NotificationTemplateId = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateCode = normalizedCode,
                CurrentVersion = 0,
                Enabled = true,
                UpdatedBy = actor,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.NotificationTemplates.Add(template);
        }

        var nextVersion = template.CurrentVersion + 1;
        var version = new NotificationTemplateVersionEntity
        {
            NotificationTemplateVersionId = Guid.NewGuid(),
            NotificationTemplateId = template.NotificationTemplateId,
            TenantId = tenantId,
            TemplateCode = normalizedCode,
            Version = nextVersion,
            Title = normalizedTitle,
            Content = normalizedContent,
            ChangedBy = actor,
            ChangedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationTemplateVersions.Add(version);

        template.CurrentVersion = nextVersion;
        template.UpdatedBy = actor;
        template.UpdatedAt = version.ChangedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(template, version);
    }

    public async Task<IReadOnlyCollection<NotificationTemplateSummary>> GetTemplatesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.NotificationTemplates
            .Where(x => x.TenantId == tenantContextAccessor.TenantId)
            .OrderBy(x => x.TemplateCode)
            .Select(x => new NotificationTemplateSummary(
                x.TemplateCode,
                x.CurrentVersion,
                x.Enabled,
                x.UpdatedBy,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<NotificationTemplateDetail?> GetTemplateAsync(string templateCode, int? version, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(templateCode);
        var tenantId = tenantContextAccessor.TenantId;
        var template = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == normalizedCode,
            cancellationToken);
        if (template is null)
        {
            return null;
        }

        var targetVersion = version ?? template.CurrentVersion;
        var selectedVersion = await dbContext.NotificationTemplateVersions.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == normalizedCode && x.Version == targetVersion,
            cancellationToken);
        return selectedVersion is null ? null : ToDetail(template, selectedVersion);
    }

    public async Task<NotificationTemplateSummary> SetEnabledAsync(string templateCode, bool enabled, string changedBy, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(templateCode);
        var tenantId = tenantContextAccessor.TenantId;
        var template = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == normalizedCode,
            cancellationToken);
        if (template is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"模板不存在: {normalizedCode}", 404);
        }

        template.Enabled = enabled;
        template.UpdatedBy = NormalizeActor(changedBy);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationTemplateSummary(
            template.TemplateCode,
            template.CurrentVersion,
            template.Enabled,
            template.UpdatedBy,
            template.UpdatedAt);
    }

    public async Task<IReadOnlyCollection<NotificationTemplateVersionSummary>> GetVersionsAsync(
        string templateCode,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(templateCode);
        return await dbContext.NotificationTemplateVersions
            .Where(x => x.TenantId == tenantContextAccessor.TenantId && x.TemplateCode == normalizedCode)
            .OrderByDescending(x => x.Version)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new NotificationTemplateVersionSummary(
                x.Version,
                x.Title,
                x.ChangedBy,
                x.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationTemplateDiffResult> DiffVersionsAsync(
        string templateCode,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(templateCode);
        var tenantId = tenantContextAccessor.TenantId;
        var versions = await dbContext.NotificationTemplateVersions
            .Where(x =>
                x.TenantId == tenantId &&
                x.TemplateCode == normalizedCode &&
                (x.Version == fromVersion || x.Version == toVersion))
            .ToListAsync(cancellationToken);
        var from = versions.SingleOrDefault(x => x.Version == fromVersion);
        var to = versions.SingleOrDefault(x => x.Version == toVersion);
        if (from is null || to is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"未找到指定版本：from={fromVersion}, to={toVersion}", 404);
        }

        var titleChanged = !string.Equals(from.Title, to.Title, StringComparison.Ordinal);
        var contentTokensFrom = TokenizeWords(from.Content);
        var contentTokensTo = TokenizeWords(to.Content);
        var addedTokens = contentTokensTo.Except(contentTokensFrom, StringComparer.OrdinalIgnoreCase).ToArray();
        var removedTokens = contentTokensFrom.Except(contentTokensTo, StringComparer.OrdinalIgnoreCase).ToArray();
        return new NotificationTemplateDiffResult(
            normalizedCode,
            fromVersion,
            toVersion,
            from.Title,
            to.Title,
            from.Content,
            to.Content,
            titleChanged,
            addedTokens,
            removedTokens,
            !titleChanged && addedTokens.Length == 0 && removedTokens.Length == 0);
    }

    public async Task<NotificationTemplateRenderedContent> RenderAsync(
        string templateCode,
        int? version,
        IReadOnlyDictionary<string, string>? variables,
        bool allowDisabledTemplate = false,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetTemplateAsync(templateCode, version, cancellationToken);
        if (detail is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"模板不存在: {templateCode}", 404);
        }
        if (!allowDisabledTemplate && !detail.Enabled)
        {
            throw new AppException(ErrorCodes.ValidationError, $"模板已停用: {templateCode}", 400);
        }

        ValidateRequiredVariables(detail.Title, detail.Content, variables);
        var renderedTitle = ApplyVariables(detail.Title, variables);
        var renderedContent = ApplyVariables(detail.Content, variables);
        return new NotificationTemplateRenderedContent(
            detail.TemplateCode,
            detail.Version,
            renderedTitle,
            renderedContent);
    }

    public async Task<IReadOnlyCollection<string>> GetVariablesAsync(
        string templateCode,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetTemplateAsync(templateCode, version, cancellationToken);
        if (detail is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"模板不存在: {templateCode}", 404);
        }
        return ExtractVariables(detail.Title, detail.Content);
    }

    public async Task<NotificationTemplateDetail> RollbackAsync(
        string templateCode,
        int targetVersion,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(templateCode);
        var tenantId = tenantContextAccessor.TenantId;
        var template = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == normalizedCode,
            cancellationToken);
        if (template is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"模板不存在: {normalizedCode}", 404);
        }

        var target = await dbContext.NotificationTemplateVersions.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.TemplateCode == normalizedCode && x.Version == targetVersion,
            cancellationToken);
        if (target is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"未找到目标版本: {targetVersion}", 404);
        }

        var actor = NormalizeActor(changedBy);
        var now = DateTimeOffset.UtcNow;
        var newVersionNumber = template.CurrentVersion + 1;
        var version = new NotificationTemplateVersionEntity
        {
            NotificationTemplateVersionId = Guid.NewGuid(),
            NotificationTemplateId = template.NotificationTemplateId,
            TenantId = tenantId,
            TemplateCode = normalizedCode,
            Version = newVersionNumber,
            Title = target.Title,
            Content = target.Content,
            ChangedBy = actor,
            ChangedAt = now
        };
        dbContext.NotificationTemplateVersions.Add(version);

        template.CurrentVersion = newVersionNumber;
        template.UpdatedBy = actor;
        template.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(template, version);
    }

    private static NotificationTemplateDetail ToDetail(NotificationTemplateEntity template, NotificationTemplateVersionEntity version) =>
        new(
            template.TemplateCode,
            version.Version,
            template.Enabled,
            version.Title,
            version.Content,
            version.ChangedBy,
            version.ChangedAt,
            template.UpdatedBy,
            template.UpdatedAt);

    private static string NormalizeCode(string code)
    {
        var normalized = code.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppException(ErrorCodes.ValidationError, "templateCode 不能为空。");
        }
        if (normalized.Length > 128)
        {
            throw new AppException(ErrorCodes.ValidationError, "templateCode 长度不能超过 128。");
        }
        return normalized;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppException(ErrorCodes.ValidationError, "title 不能为空。");
        }
        if (normalized.Length > 200)
        {
            throw new AppException(ErrorCodes.ValidationError, "title 长度不能超过 200。");
        }
        return normalized;
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppException(ErrorCodes.ValidationError, "content 不能为空。");
        }
        if (normalized.Length > 4000)
        {
            throw new AppException(ErrorCodes.ValidationError, "content 长度不能超过 4000。");
        }
        return normalized;
    }

    private static string NormalizeActor(string actor)
    {
        var normalized = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }

    private static IReadOnlyCollection<string> TokenizeWords(string input) =>
        Regex.Matches(input, @"[\p{L}\p{N}_]+")
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ApplyVariables(string template, IReadOnlyDictionary<string, string>? variables)
    {
        if (variables is null || variables.Count == 0)
        {
            return template;
        }

        return Regex.Replace(template, @"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static void ValidateRequiredVariables(string title, string content, IReadOnlyDictionary<string, string>? variables)
    {
        var required = ExtractVariables(title, content);
        if (required.Count == 0)
        {
            return;
        }

        var missing = required
            .Where(key => variables is null || !variables.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"变量缺失: {string.Join(", ", missing)}。");
        }
    }

    private static IReadOnlyCollection<string> ExtractVariables(string title, string content)
    {
        var all = $"{title}\n{content}";
        return Regex.Matches(all, @"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")
            .Select(x => x.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record NotificationTemplateSummary(
    string TemplateCode,
    int CurrentVersion,
    bool Enabled,
    string UpdatedBy,
    DateTimeOffset UpdatedAt);
public sealed record NotificationTemplateVersionSummary(
    int Version,
    string Title,
    string ChangedBy,
    DateTimeOffset ChangedAt);
public sealed record NotificationTemplateDetail(
    string TemplateCode,
    int Version,
    bool Enabled,
    string Title,
    string Content,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string UpdatedBy,
    DateTimeOffset UpdatedAt);
public sealed record NotificationTemplateDiffResult(
    string TemplateCode,
    int FromVersion,
    int ToVersion,
    string FromTitle,
    string ToTitle,
    string FromContent,
    string ToContent,
    bool TitleChanged,
    IReadOnlyCollection<string> AddedTokens,
    IReadOnlyCollection<string> RemovedTokens,
    bool IsSemanticallySame);
public sealed record NotificationTemplateRenderedContent(
    string TemplateCode,
    int Version,
    string Title,
    string Content);
