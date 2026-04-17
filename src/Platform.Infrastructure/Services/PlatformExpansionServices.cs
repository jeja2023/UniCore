using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Platform.Core.Common;

namespace Platform.Infrastructure.Services;

public sealed class ObjectStorageOptions
{
    public const string Section = "ObjectStorage";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "unicore-files";
    public bool UseSsl { get; set; }
    public bool ForcePathStyle { get; set; } = true;
}

public sealed class S3FileStorage(ObjectStorageOptions options) : IFileStorage
{
    private readonly AmazonS3Client _client = BuildClient(options);
    private readonly string _bucketName = options.BucketName;

    public async Task<StoredFileInfo> SaveAsync(
        Stream content,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var fileId = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        await EnsureBucketExistsAsync(cancellationToken);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileId,
            InputStream = content,
            ContentType = contentType ?? "application/octet-stream",
            AutoCloseStream = false
        }, cancellationToken);

        return new StoredFileInfo(fileId, fileName, contentType ?? "application/octet-stream", content.CanSeek ? content.Length : 0, DateTimeOffset.UtcNow);
    }

    public async Task<StoredFileContent?> ReadAsync(string fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucketName, fileId, cancellationToken);
            await using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken);
            return new StoredFileContent(fileId, fileId, response.Headers.ContentType ?? "application/octet-stream", ms.ToArray());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucketName);
        if (!exists)
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, cancellationToken);
        }
    }

    private static AmazonS3Client BuildClient(ObjectStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("ObjectStorage:Endpoint 不能为空。");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = options.ForcePathStyle,
            UseHttp = !options.UseSsl
        };
        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        return new AmazonS3Client(credentials, config);
    }
}

public sealed class TrackingFileStorage(
    IFileStorage innerStorage,
    IServiceScopeFactory scopeFactory,
    ITenantContextAccessor tenantContextAccessor,
    string provider,
    string bucket) : IFileStorage
{
    public async Task<StoredFileInfo> SaveAsync(Stream content, string fileName, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var stored = await innerStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.StoredFileObjects.Add(new StoredFileObjectEntity
        {
            StoredFileObjectId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            FileId = stored.FileId,
            Provider = provider,
            Bucket = bucket,
            ObjectKey = stored.FileId,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            Length = stored.Length,
            StoredAt = stored.StoredAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return stored;
    }

    public Task<StoredFileContent?> ReadAsync(string fileId, CancellationToken cancellationToken = default) =>
        innerStorage.ReadAsync(fileId, cancellationToken);
}

public sealed class TenantService(AppDbContext dbContext)
{
    public async Task<IReadOnlyCollection<TenantEntity>> GetTenantsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Tenants.OrderBy(x => x.TenantId).ToListAsync(cancellationToken);

    public async Task<TenantEntity> CreateTenantAsync(string tenantId, string tenantName, CancellationToken cancellationToken = default)
    {
        var normalized = tenantId.Trim();
        var exists = await dbContext.Tenants.AnyAsync(x => x.TenantId == normalized, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"租户已存在: {normalized}");
        }

        var entity = new TenantEntity
        {
            TenantId = normalized,
            TenantName = tenantName.Trim(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Tenants.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = tenantId.Trim();
        var exists = await dbContext.Tenants.AnyAsync(x => x.TenantId == normalized, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"租户不存在: {normalized}");
        }

        var settings = await dbContext.TenantSettings
            .Where(x => x.TenantId == normalized)
            .OrderBy(x => x.SettingKey)
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, cancellationToken);
        return settings;
    }

    public async Task<TenantSettingEntity> UpsertSettingAsync(string tenantId, string settingKey, string settingValue, CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = tenantId.Trim();
        var normalizedKey = settingKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new InvalidOperationException("settingKey 不能为空。");
        }
        if (normalizedKey.Length > 128)
        {
            throw new InvalidOperationException("settingKey 长度不能超过 128。");
        }

        var tenantExists = await dbContext.Tenants.AnyAsync(x => x.TenantId == normalizedTenantId, cancellationToken);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"租户不存在: {normalizedTenantId}");
        }

        var entity = await dbContext.TenantSettings.SingleOrDefaultAsync(
            x => x.TenantId == normalizedTenantId && x.SettingKey == normalizedKey,
            cancellationToken);
        if (entity is null)
        {
            entity = new TenantSettingEntity
            {
                TenantSettingId = Guid.NewGuid(),
                TenantId = normalizedTenantId,
                SettingKey = normalizedKey,
                SettingValue = settingValue,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.TenantSettings.Add(entity);
        }
        else
        {
            entity.SettingValue = settingValue;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

public sealed class ExternalIdentityLinkService(AppDbContext dbContext)
{
    public async Task<Guid?> FindUserIdAsync(string tenantId, string provider, string externalUserId, CancellationToken cancellationToken = default)
    {
        var link = await dbContext.ExternalIdentityLinks.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Provider == provider && x.ExternalUserId == externalUserId,
            cancellationToken);
        return link?.UserId;
    }

    public async Task LinkAsync(string tenantId, string provider, string externalUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.ExternalIdentityLinks.AnyAsync(
            x => x.TenantId == tenantId && x.Provider == provider && x.ExternalUserId == externalUserId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.ExternalIdentityLinks.Add(new ExternalIdentityLinkEntity
        {
            ExternalIdentityLinkId = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider,
            ExternalUserId = externalUserId,
            UserId = userId,
            LinkedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public enum DataScope
{
    Self,
    Department,
    Tenant,
    Custom
}

public sealed class DataScopeService(AppDbContext dbContext)
{
    private static readonly HashSet<string> AllowedCustomScopeFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenant_id",
        "user_id",
        "department_code"
    };

    private static readonly Regex InvalidCharacterPattern = new("[^a-zA-Z0-9_\\s\\(\\)\\&\\|=!'<>\"]", RegexOptions.Compiled);
    private static readonly Regex FieldPattern = new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

    public async Task<DataScopeResolution> ResolveScopeAsync(Guid userId, string tenantId, CancellationToken cancellationToken = default)
    {
        var scopes = await (from ur in dbContext.UserRoles
                            join ds in dbContext.RoleDataScopes on ur.RoleId equals ds.RoleId
                            where ur.UserId == userId
                            select new { ds.Scope, ds.CustomExpression })
            .ToArrayAsync(cancellationToken);
        var departmentCode = await dbContext.Users
            .Where(x => x.UserId == userId && x.TenantId == tenantId)
            .Select(x => x.DepartmentCode)
            .SingleOrDefaultAsync(cancellationToken) ?? "default";

        if (scopes.Any(x => string.Equals(x.Scope, nameof(DataScope.Custom), StringComparison.OrdinalIgnoreCase)))
        {
            var expression = scopes
                .Where(x => string.Equals(x.Scope, nameof(DataScope.Custom), StringComparison.OrdinalIgnoreCase))
                .Select(x => x.CustomExpression)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return new DataScopeResolution(DataScope.Custom, departmentCode, expression);
        }
        if (scopes.Any(x => string.Equals(x.Scope, nameof(DataScope.Tenant), StringComparison.OrdinalIgnoreCase)))
        {
            return new DataScopeResolution(DataScope.Tenant, departmentCode, null);
        }
        if (scopes.Any(x => string.Equals(x.Scope, nameof(DataScope.Department), StringComparison.OrdinalIgnoreCase)))
        {
            return new DataScopeResolution(DataScope.Department, departmentCode, null);
        }
        return new DataScopeResolution(DataScope.Self, departmentCode, null);
    }

    public async Task SetRoleScopeAsync(string roleCode, string tenantId, DataScope scope, string? customExpression = null, string changedBy = "system", int? expectedRevision = null, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.RoleCode == roleCode, cancellationToken);
        if (role is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"角色不存在: {roleCode}", 404);
        }
        if (scope == DataScope.Custom && string.IsNullOrWhiteSpace(customExpression))
        {
            throw new AppException(ErrorCodes.ValidationError, "自定义数据权限必须提供 customExpression。");
        }
        if (scope == DataScope.Custom)
        {
            ValidateCustomExpressionCore(customExpression!);
        }

        var dataScope = await dbContext.RoleDataScopes.SingleOrDefaultAsync(x => x.RoleId == role.RoleId, cancellationToken);
        if (dataScope is null)
        {
            if (expectedRevision.HasValue && expectedRevision.Value != 0)
            {
                throw new AppException(ErrorCodes.ValidationError, $"数据权限版本冲突：当前版本为 0，请刷新后重试。", 409);
            }
            dataScope = new RoleDataScopeEntity { RoleId = role.RoleId, Scope = scope.ToString() };
            dbContext.RoleDataScopes.Add(dataScope);
        }
        else
        {
            if (expectedRevision.HasValue && expectedRevision.Value != dataScope.Revision)
            {
                throw new AppException(
                    ErrorCodes.ValidationError,
                    $"数据权限版本冲突：期望版本 {expectedRevision.Value}，当前版本 {dataScope.Revision}。",
                    409);
            }
            dataScope.Scope = scope.ToString();
        }
        dataScope.CustomExpression = scope == DataScope.Custom ? customExpression?.Trim() : null;
        dataScope.Revision += 1;
        dataScope.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await AppendHistoryAsync(role.RoleId, dataScope.Scope, dataScope.CustomExpression, changedBy, cancellationToken);
    }

    public async Task<RoleDataScopeDetail?> GetRoleScopeAsync(string roleCode, string tenantId, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.RoleCode == roleCode,
            cancellationToken);
        if (role is null)
        {
            return null;
        }

        var scope = await dbContext.RoleDataScopes.SingleOrDefaultAsync(x => x.RoleId == role.RoleId, cancellationToken);
        if (scope is null)
        {
            return new RoleDataScopeDetail(role.RoleCode, DataScope.Self.ToString(), null, 0, null);
        }
        return new RoleDataScopeDetail(role.RoleCode, scope.Scope, scope.CustomExpression, scope.Revision, scope.UpdatedAt);
    }

    public DataScopeExpressionValidationResult ValidateCustomExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new DataScopeExpressionValidationResult(false, "customExpression 不能为空。");
        }

        try
        {
            ValidateCustomExpressionCore(expression);
            return new DataScopeExpressionValidationResult(true, null);
        }
        catch (AppException ex)
        {
            return new DataScopeExpressionValidationResult(false, ex.Message);
        }
    }

    public string ComposeCustomExpression(IReadOnlyCollection<DataScopeExpressionRule> rules)
    {
        if (rules.Count == 0)
        {
            throw new AppException(ErrorCodes.ValidationError, "rules 不能为空。");
        }

        var builder = new StringBuilder();
        var groupDepth = 0;
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules.ElementAt(i);
            if (!AllowedCustomScopeFields.Contains(rule.Field))
            {
                throw new AppException(ErrorCodes.ValidationError, $"字段不支持: {rule.Field}");
            }

            var op = NormalizeOperator(rule.Operator);
            var join = NormalizeJoiner(rule.JoinWithPrevious, i);
            var value = EscapeValue(rule.Value);
            if (i > 0)
            {
                builder.Append(' ').Append(join).Append(' ');
            }
            if (rule.OpenGroupCount < 0 || rule.CloseGroupCount < 0)
            {
                throw new AppException(ErrorCodes.ValidationError, "分组参数不能为负数。");
            }
            if (rule.OpenGroupCount > 0)
            {
                builder.Append(new string('(', rule.OpenGroupCount));
                groupDepth += rule.OpenGroupCount;
            }
            builder.Append(rule.Field).Append(' ').Append(op).Append(" '").Append(value).Append('\'');
            if (rule.CloseGroupCount > 0)
            {
                groupDepth -= rule.CloseGroupCount;
                if (groupDepth < 0)
                {
                    throw new AppException(ErrorCodes.ValidationError, "分组右括号数量超过左括号。");
                }
                builder.Append(new string(')', rule.CloseGroupCount));
            }
        }
        if (groupDepth != 0)
        {
            throw new AppException(ErrorCodes.ValidationError, "分组括号未闭合。");
        }

        var expression = builder.ToString();
        ValidateCustomExpressionCore(expression);
        return expression;
    }

    public async Task<IReadOnlyCollection<RoleDataScopeHistoryItem>> GetRoleScopeHistoryAsync(
        string roleCode,
        string tenantId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.RoleCode == roleCode,
            cancellationToken);
        if (role is null)
        {
            return [];
        }

        return await dbContext.RoleDataScopeHistories
            .Where(x => x.RoleId == role.RoleId)
            .OrderByDescending(x => x.Version)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new RoleDataScopeHistoryItem(
                x.RoleDataScopeHistoryId,
                x.Version,
                x.Scope,
                x.CustomExpression,
                x.ChangedBy,
                x.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDataScopeDetail> RollbackRoleScopeAsync(
        string roleCode,
        string tenantId,
        int targetVersion,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.RoleCode == roleCode,
            cancellationToken);
        if (role is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"角色不存在: {roleCode}", 404);
        }

        var target = await dbContext.RoleDataScopeHistories
            .Where(x => x.RoleId == role.RoleId && x.Version == targetVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"未找到目标版本: {targetVersion}", 404);
        }

        if (!Enum.TryParse<DataScope>(target.Scope, true, out var parsedScope))
        {
            throw new AppException(ErrorCodes.ValidationError, $"历史版本 scope 非法: {target.Scope}");
        }

        await SetRoleScopeAsync(roleCode, tenantId, parsedScope, target.CustomExpression, changedBy, null, cancellationToken);
        var current = await dbContext.RoleDataScopes.SingleAsync(x => x.RoleId == role.RoleId, cancellationToken);
        return new RoleDataScopeDetail(roleCode, target.Scope, target.CustomExpression, current.Revision, current.UpdatedAt);
    }

    public async Task<RoleDataScopeDiffResult> DiffRoleScopeVersionsAsync(
        string roleCode,
        string tenantId,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.RoleCode == roleCode,
            cancellationToken);
        if (role is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"角色不存在: {roleCode}", 404);
        }

        var histories = await dbContext.RoleDataScopeHistories
            .Where(x => x.RoleId == role.RoleId && (x.Version == fromVersion || x.Version == toVersion))
            .ToListAsync(cancellationToken);
        var from = histories.SingleOrDefault(x => x.Version == fromVersion);
        var to = histories.SingleOrDefault(x => x.Version == toVersion);
        if (from is null || to is null)
        {
            throw new AppException(ErrorCodes.NotFound, $"未找到指定版本：from={fromVersion}, to={toVersion}", 404);
        }

        var fromTokens = TokenizeExpression(from.CustomExpression);
        var toTokens = TokenizeExpression(to.CustomExpression);
        var added = toTokens.Except(fromTokens, StringComparer.OrdinalIgnoreCase).ToArray();
        var removed = fromTokens.Except(toTokens, StringComparer.OrdinalIgnoreCase).ToArray();
        var summary = BuildDiffSummary(from.Scope, to.Scope, added, removed);
        return new RoleDataScopeDiffResult(
            roleCode,
            from.Version,
            to.Version,
            from.Scope,
            to.Scope,
            from.CustomExpression,
            to.CustomExpression,
            added,
            removed,
            added.Length == 0 && removed.Length == 0 && string.Equals(from.Scope, to.Scope, StringComparison.OrdinalIgnoreCase),
            summary);
    }

    public DataScopeParseResult ParseExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new DataScopeParseResult(false, "customExpression 不能为空。", []);
        }

        var validation = ValidateCustomExpression(expression);
        if (!validation.IsValid)
        {
            return new DataScopeParseResult(false, validation.ErrorMessage, []);
        }

        var tokens = TokenizeWithKind(expression!);
        return new DataScopeParseResult(true, null, tokens);
    }

    public IQueryable<UserEntity> ApplyUserFilter(
        IQueryable<UserEntity> query,
        DataScopeResolution resolution,
        Guid currentUserId,
        string currentTenantId)
    {
        if (resolution.Scope == DataScope.Self)
        {
            return query.Where(x => x.UserId == currentUserId);
        }
        if (resolution.Scope == DataScope.Department)
        {
            return query.Where(x => x.DepartmentCode == resolution.DepartmentCode);
        }
        if (resolution.Scope == DataScope.Tenant)
        {
            return query.Where(x => x.TenantId == currentTenantId);
        }
        if (resolution.Scope != DataScope.Custom || string.IsNullOrWhiteSpace(resolution.CustomExpression))
        {
            return query;
        }

        var predicate = BuildUserPredicate(resolution.CustomExpression!, currentUserId, currentTenantId, resolution.DepartmentCode);
        return query.Where(predicate);
    }

    private static void ValidateCustomExpressionCore(string expression)
    {
        var normalized = expression.Trim();
        if (normalized.Length > 512)
        {
            throw new AppException(ErrorCodes.ValidationError, "customExpression 长度不能超过 512。");
        }
        if (InvalidCharacterPattern.IsMatch(normalized))
        {
            throw new AppException(ErrorCodes.ValidationError, "customExpression 包含非法字符。");
        }
        if (!IsParenthesesBalanced(normalized))
        {
            throw new AppException(ErrorCodes.ValidationError, "customExpression 括号不匹配。");
        }

        var normalizedWithoutStrings = RemoveQuotedLiterals(normalized);
        var tokens = FieldPattern.Matches(normalizedWithoutStrings)
            .Select(x => x.Value)
            .Where(x => !IsKeyword(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var invalidFields = tokens
            .Where(x => !AllowedCustomScopeFields.Contains(x))
            .ToArray();
        if (invalidFields.Length > 0)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"customExpression 包含未授权字段：{string.Join(", ", invalidFields)}。仅支持 tenant_id/user_id/department_code。");
        }
    }

    private static System.Linq.Expressions.Expression<Func<UserEntity, bool>> BuildUserPredicate(
        string customExpression,
        Guid currentUserId,
        string currentTenantId,
        string currentDepartmentCode)
    {
        ValidateCustomExpressionCore(customExpression);
        var parser = new DataScopeExpressionParser(customExpression, currentUserId, currentTenantId, currentDepartmentCode);
        return parser.ParseUserPredicate();
    }

    private static string NormalizeOperator(string op)
    {
        var normalized = op.Trim();
        return normalized switch
        {
            "=" or "==" => "=",
            "!=" or "<>" => "!=",
            ">" or ">=" or "<" or "<=" => normalized,
            _ => throw new AppException(ErrorCodes.ValidationError, $"操作符不支持: {op}")
        };
    }

    private static string NormalizeJoiner(string? joiner, int index)
    {
        if (index == 0)
        {
            return "AND";
        }
        if (string.IsNullOrWhiteSpace(joiner))
        {
            return "AND";
        }
        return joiner.Trim().ToUpperInvariant() switch
        {
            "AND" => "AND",
            "OR" => "OR",
            _ => throw new AppException(ErrorCodes.ValidationError, $"连接符不支持: {joiner}")
        };
    }

    private static string EscapeValue(string? input) => (input ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);

    private static string[] TokenizeExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }
        var normalized = RemoveQuotedLiterals(expression);
        return Regex.Matches(normalized, @"[a-zA-Z_][a-zA-Z0-9_]*|!=|>=|<=|=|>|<|\(|\)")
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<DataScopeParsedToken> TokenizeWithKind(string expression)
    {
        var raw = Regex.Matches(expression, @"'[^']*(?:''[^']*)*'|!=|>=|<=|=|>|<|\(|\)|\bAND\b|\bOR\b|[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.IgnoreCase)
            .Select(x => x.Value)
            .ToArray();

        return raw.Select(token =>
        {
            if (string.Equals(token, "AND", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "OR", StringComparison.OrdinalIgnoreCase))
            {
                return new DataScopeParsedToken(token.ToUpperInvariant(), "joiner");
            }
            if (token is "=" or "!=" or ">" or ">=" or "<" or "<=")
            {
                return new DataScopeParsedToken(token, "operator");
            }
            if (token is "(" or ")")
            {
                return new DataScopeParsedToken(token, "group");
            }
            if (token.StartsWith('\'') && token.EndsWith('\''))
            {
                return new DataScopeParsedToken(token, "literal");
            }
            if (AllowedCustomScopeFields.Contains(token))
            {
                return new DataScopeParsedToken(token, "field");
            }
            return new DataScopeParsedToken(token, "identifier");
        }).ToArray();
    }

    private static string BuildDiffSummary(string fromScope, string toScope, IReadOnlyCollection<string> added, IReadOnlyCollection<string> removed)
    {
        var chunks = new List<string>();
        if (!string.Equals(fromScope, toScope, StringComparison.OrdinalIgnoreCase))
        {
            chunks.Add($"scope {fromScope} -> {toScope}");
        }
        if (added.Count > 0)
        {
            chunks.Add($"新增: {string.Join(", ", added)}");
        }
        if (removed.Count > 0)
        {
            chunks.Add($"移除: {string.Join(", ", removed)}");
        }
        return chunks.Count == 0 ? "无差异" : string.Join("；", chunks);
    }

    private async Task AppendHistoryAsync(Guid roleId, string scope, string? customExpression, string changedBy, CancellationToken cancellationToken)
    {
        var latestVersion = await dbContext.RoleDataScopeHistories
            .Where(x => x.RoleId == roleId)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken) ?? 0;

        dbContext.RoleDataScopeHistories.Add(new RoleDataScopeHistoryEntity
        {
            RoleDataScopeHistoryId = Guid.NewGuid(),
            RoleId = roleId,
            Scope = scope,
            CustomExpression = customExpression,
            Version = latestVersion + 1,
            ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy.Trim(),
            ChangedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsKeyword(string token) =>
        string.Equals(token, "and", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, "or", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, "false", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, "null", StringComparison.OrdinalIgnoreCase);

    private static bool IsParenthesesBalanced(string input)
    {
        var depth = 0;
        foreach (var ch in input)
        {
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }
        }
        return depth == 0;
    }

    private static string RemoveQuotedLiterals(string input)
    {
        var builder = new StringBuilder(input.Length);
        var inQuote = false;
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '\'')
            {
                if (inQuote && i + 1 < input.Length && input[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                inQuote = !inQuote;
                continue;
            }

            if (!inQuote)
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }
}

file sealed class DataScopeExpressionParser
{
    private readonly string[] _tokens;
    private int _pos;
    private readonly Guid _currentUserId;
    private readonly string _currentTenantId;
    private readonly string _currentDepartmentCode;

    public DataScopeExpressionParser(string expression, Guid currentUserId, string currentTenantId, string currentDepartmentCode)
    {
        _tokens = Regex.Matches(expression, @"'[^']*(?:''[^']*)*'|!=|>=|<=|=|>|<|\(|\)|\bAND\b|\bOR\b|[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.IgnoreCase)
            .Select(x => x.Value)
            .ToArray();
        _currentUserId = currentUserId;
        _currentTenantId = currentTenantId;
        _currentDepartmentCode = currentDepartmentCode;
    }

    public System.Linq.Expressions.Expression<Func<UserEntity, bool>> ParseUserPredicate()
    {
        var param = System.Linq.Expressions.Expression.Parameter(typeof(UserEntity), "x");
        var body = ParseOr(param);
        if (_pos != _tokens.Length)
        {
            throw new AppException(ErrorCodes.ValidationError, "customExpression 解析失败：存在无法识别的尾部 token。");
        }
        return System.Linq.Expressions.Expression.Lambda<Func<UserEntity, bool>>(body, param);
    }

    private System.Linq.Expressions.Expression ParseOr(System.Linq.Expressions.ParameterExpression param)
    {
        var left = ParseAnd(param);
        while (TryConsumeKeyword("OR"))
        {
            var right = ParseAnd(param);
            left = System.Linq.Expressions.Expression.OrElse(left, right);
        }
        return left;
    }

    private System.Linq.Expressions.Expression ParseAnd(System.Linq.Expressions.ParameterExpression param)
    {
        var left = ParseFactor(param);
        while (TryConsumeKeyword("AND"))
        {
            var right = ParseFactor(param);
            left = System.Linq.Expressions.Expression.AndAlso(left, right);
        }
        return left;
    }

    private System.Linq.Expressions.Expression ParseFactor(System.Linq.Expressions.ParameterExpression param)
    {
        if (TryConsume("("))
        {
            var inner = ParseOr(param);
            if (!TryConsume(")"))
            {
                throw new AppException(ErrorCodes.ValidationError, "customExpression 解析失败：缺少右括号。");
            }
            return inner;
        }

        return ParseComparison(param);
    }

    private System.Linq.Expressions.Expression ParseComparison(System.Linq.Expressions.ParameterExpression param)
    {
        var field = ConsumeIdentifier("字段");
        var op = ConsumeOperator();
        var literal = ConsumeLiteral();
        var value = ResolvePlaceholder(literal);

        return field.ToLowerInvariant() switch
        {
            "tenant_id" => BuildStringComparison(param, nameof(UserEntity.TenantId), op, value),
            "department_code" => BuildStringComparison(param, nameof(UserEntity.DepartmentCode), op, value),
            "user_id" => BuildGuidComparison(param, nameof(UserEntity.UserId), op, value),
            _ => throw new AppException(ErrorCodes.ValidationError, $"customExpression 解析失败：字段不支持: {field}")
        };
    }

    private static System.Linq.Expressions.Expression BuildStringComparison(
        System.Linq.Expressions.ParameterExpression param,
        string propertyName,
        string op,
        string value)
    {
        var member = System.Linq.Expressions.Expression.Property(param, propertyName);
        var constant = System.Linq.Expressions.Expression.Constant(value, typeof(string));
        return op switch
        {
            "=" => System.Linq.Expressions.Expression.Equal(member, constant),
            "!=" => System.Linq.Expressions.Expression.NotEqual(member, constant),
            ">" or ">=" or "<" or "<=" => BuildStringOrdering(member, constant, op),
            _ => throw new AppException(ErrorCodes.ValidationError, $"customExpression 操作符不支持: {op}")
        };
    }

    private static System.Linq.Expressions.Expression BuildStringOrdering(
        System.Linq.Expressions.Expression left,
        System.Linq.Expressions.Expression right,
        string op)
    {
        var compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string), typeof(StringComparison)])
                      ?? throw new InvalidOperationException("string.Compare not found.");
        var call = System.Linq.Expressions.Expression.Call(compare, left, right, System.Linq.Expressions.Expression.Constant(StringComparison.Ordinal));
        var zero = System.Linq.Expressions.Expression.Constant(0);
        return op switch
        {
            ">" => System.Linq.Expressions.Expression.GreaterThan(call, zero),
            ">=" => System.Linq.Expressions.Expression.GreaterThanOrEqual(call, zero),
            "<" => System.Linq.Expressions.Expression.LessThan(call, zero),
            "<=" => System.Linq.Expressions.Expression.LessThanOrEqual(call, zero),
            _ => throw new AppException(ErrorCodes.ValidationError, $"customExpression 操作符不支持: {op}")
        };
    }

    private static System.Linq.Expressions.Expression BuildGuidComparison(
        System.Linq.Expressions.ParameterExpression param,
        string propertyName,
        string op,
        string value)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new AppException(ErrorCodes.ValidationError, $"customExpression user_id 值必须是 GUID: {value}");
        }

        var member = System.Linq.Expressions.Expression.Property(param, propertyName);
        var constant = System.Linq.Expressions.Expression.Constant(guid, typeof(Guid));
        return op switch
        {
            "=" => System.Linq.Expressions.Expression.Equal(member, constant),
            "!=" => System.Linq.Expressions.Expression.NotEqual(member, constant),
            _ => throw new AppException(ErrorCodes.ValidationError, "customExpression 中 user_id 仅支持 = 或 !=。")
        };
    }

    private string ResolvePlaceholder(string raw)
    {
        var value = raw;
        if (string.Equals(value, "{current_user_id}", StringComparison.OrdinalIgnoreCase))
        {
            return _currentUserId.ToString();
        }
        if (string.Equals(value, "{current_tenant_id}", StringComparison.OrdinalIgnoreCase))
        {
            return _currentTenantId;
        }
        if (string.Equals(value, "{current_department_code}", StringComparison.OrdinalIgnoreCase))
        {
            return _currentDepartmentCode;
        }
        return value;
    }

    private bool TryConsumeKeyword(string keyword)
    {
        if (_pos >= _tokens.Length)
        {
            return false;
        }
        if (string.Equals(_tokens[_pos], keyword, StringComparison.OrdinalIgnoreCase))
        {
            _pos++;
            return true;
        }
        return false;
    }

    private bool TryConsume(string token)
    {
        if (_pos >= _tokens.Length)
        {
            return false;
        }
        if (string.Equals(_tokens[_pos], token, StringComparison.Ordinal))
        {
            _pos++;
            return true;
        }
        return false;
    }

    private string ConsumeIdentifier(string displayName)
    {
        if (_pos >= _tokens.Length)
        {
            throw new AppException(ErrorCodes.ValidationError, $"customExpression 解析失败：缺少{displayName}。");
        }
        var token = _tokens[_pos];
        if (!Regex.IsMatch(token, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            throw new AppException(ErrorCodes.ValidationError, $"customExpression 解析失败：{displayName}非法: {token}");
        }
        _pos++;
        return token;
    }

    private string ConsumeOperator()
    {
        if (_pos >= _tokens.Length)
        {
            throw new AppException(ErrorCodes.ValidationError, "customExpression 解析失败：缺少操作符。");
        }
        var token = _tokens[_pos];
        _pos++;
        return token switch
        {
            "=" or "!=" or ">" or ">=" or "<" or "<=" => token,
            _ => throw new AppException(ErrorCodes.ValidationError, $"customExpression 解析失败：操作符不支持: {token}")
        };
    }

    private string ConsumeLiteral()
    {
        if (_pos >= _tokens.Length)
        {
            throw new AppException(ErrorCodes.ValidationError, "customExpression 解析失败：缺少值。");
        }
        var token = _tokens[_pos];
        if (!token.StartsWith('\'') || !token.EndsWith('\''))
        {
            throw new AppException(ErrorCodes.ValidationError, $"customExpression 解析失败：值必须使用单引号包裹: {token}");
        }
        _pos++;
        var inner = token[1..^1];
        return inner.Replace("''", "'", StringComparison.Ordinal);
    }
}
public sealed record DataScopeResolution(DataScope Scope, string DepartmentCode, string? CustomExpression);
public sealed record RoleDataScopeDetail(string RoleCode, string Scope, string? CustomExpression, int Revision, DateTimeOffset? UpdatedAt);
public sealed record DataScopeExpressionValidationResult(bool IsValid, string? ErrorMessage);
public sealed record DataScopeExpressionRule(
    string Field,
    string Operator,
    string Value,
    string? JoinWithPrevious,
    int OpenGroupCount = 0,
    int CloseGroupCount = 0);
public sealed record RoleDataScopeHistoryItem(
    Guid HistoryId,
    int Version,
    string Scope,
    string? CustomExpression,
    string ChangedBy,
    DateTimeOffset ChangedAt);
public sealed record RoleDataScopeDiffResult(
    string RoleCode,
    int FromVersion,
    int ToVersion,
    string FromScope,
    string ToScope,
    string? FromExpression,
    string? ToExpression,
    IReadOnlyCollection<string> AddedTokens,
    IReadOnlyCollection<string> RemovedTokens,
    bool IsSemanticallySame,
    string Summary);
public sealed record DataScopeParseResult(bool IsValid, string? ErrorMessage, IReadOnlyCollection<DataScopeParsedToken> Tokens);
public sealed record DataScopeParsedToken(string Value, string Kind);

public sealed class EmailChannelOptions
{
    public const string Section = "NotificationChannels:Email";
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@unicore.local";
}

public sealed class SmsChannelOptions
{
    public const string Section = "NotificationChannels:Sms";
    public bool Enabled { get; set; }
    public string ProviderUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string SenderId { get; set; } = "UniCore";
}

public sealed class OidcSsoOptions
{
    public const string Section = "Sso:Oidc";
    public bool Enabled { get; set; }
    public List<OidcProviderConfig> Providers { get; set; } = [];
}

public sealed class OidcProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInfoEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed record OidcUserProfile(string Provider, string Subject, string? Username, string? DisplayName, string? Email);

public sealed class OidcSsoService(IHttpClientFactory httpClientFactory, IOptions<OidcSsoOptions> options)
{
    public async Task<OidcUserProfile> ExchangeCodeAsync(string provider, string code, string redirectUri, string? codeVerifier, CancellationToken cancellationToken = default)
    {
        var config = options.Value.Providers.SingleOrDefault(x => string.Equals(x.Name, provider, StringComparison.OrdinalIgnoreCase));
        if (!options.Value.Enabled || config is null)
        {
            throw new InvalidOperationException($"OIDC Provider 未配置或未启用: {provider}");
        }

        var tokenContent = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        };
        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            tokenContent["code_verifier"] = codeVerifier;
        }

        var client = httpClientFactory.CreateClient(nameof(OidcSsoService));
        using var tokenResp = await client.PostAsync(config.TokenEndpoint, new FormUrlEncodedContent(tokenContent), cancellationToken);
        if (!tokenResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC Token 交换失败: {(int)tokenResp.StatusCode}");
        }

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("OIDC Token 响应缺少 access_token。");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, config.UserInfoEndpoint);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var userInfoResp = await client.SendAsync(req, cancellationToken);
        if (!userInfoResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC UserInfo 获取失败: {(int)userInfoResp.StatusCode}");
        }
        using var userInfoDoc = JsonDocument.Parse(await userInfoResp.Content.ReadAsStringAsync(cancellationToken));
        var subject = userInfoDoc.RootElement.TryGetProperty("sub", out var sub)
            ? sub.GetString()
            : userInfoDoc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("OIDC UserInfo 缺少 sub。");
        }

        var username = userInfoDoc.RootElement.TryGetProperty("preferred_username", out var pu) ? pu.GetString() : null;
        var displayName = userInfoDoc.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        var email = userInfoDoc.RootElement.TryGetProperty("email", out var em) ? em.GetString() : null;
        return new OidcUserProfile(config.Name, subject!, username, displayName, email);
    }
}

public sealed class NotificationService(
    AppDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<EmailChannelOptions> emailOptions,
    IOptions<SmsChannelOptions> smsOptions)
{
    public async Task<NotificationMessageEntity> SendInboxAsync(string title, string content, string? receiver, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Inbox",
            Receiver = receiver,
            Title = title,
            Content = content,
            Status = "Sent",
            CreatedAt = DateTimeOffset.UtcNow,
            SentAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<NotificationMessageEntity> SendWebhookAsync(string callbackUrl, string title, string content, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Webhook",
            Receiver = callbackUrl,
            Title = title,
            Content = content,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySendWebhookAsync(message, cancellationToken);
        return message;
    }

    public async Task<NotificationMessageEntity> SendEmailAsync(string receiverEmail, string title, string content, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Email",
            Receiver = receiverEmail,
            Title = title,
            Content = content,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySendEmailAsync(message, cancellationToken);
        return message;
    }

    public async Task<NotificationMessageEntity> SendSmsAsync(string receiverPhone, string title, string content, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessageEntity
        {
            NotificationMessageId = Guid.NewGuid(),
            TenantId = tenantContextAccessor.TenantId,
            Channel = "Sms",
            Receiver = receiverPhone,
            Title = title,
            Content = content,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.NotificationMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TrySendSmsAsync(message, cancellationToken);
        return message;
    }

    public async Task<IReadOnlyCollection<NotificationMessageEntity>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        await dbContext.NotificationMessages
            .AsNoTracking()
            .Where(x => x.TenantId == tenantContextAccessor.TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

    public async Task TrySendWebhookAsync(NotificationMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Channel, "Webhook", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Receiver))
        {
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(NotificationService));
            var body = JsonSerializer.Serialize(new { message.NotificationMessageId, message.Title, message.Content, message.CreatedAt });
            using var response = await SendWithRetryAsync(
                ct => client.PostAsync(message.Receiver, new StringContent(body, Encoding.UTF8, "application/json"), ct),
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                message.Status = "Sent";
                message.SentAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            else
            {
                message.Status = "Failed";
                message.RetryCount += 1;
                message.Error = $"Webhook returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            message.Status = "Failed";
            message.RetryCount += 1;
            message.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TrySendEmailAsync(NotificationMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Channel, "Email", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Receiver))
        {
            return;
        }

        try
        {
            var options = emailOptions.Value;
            if (!options.Enabled)
            {
                throw new InvalidOperationException("Email channel disabled.");
            }

            using var smtp = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(options.Username)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(options.Username, options.Password)
            };
            using var mail = new MailMessage(options.FromAddress, message.Receiver, message.Title, message.Content);
            await smtp.SendMailAsync(mail, cancellationToken);

            message.Status = "Sent";
            message.SentAt = DateTimeOffset.UtcNow;
            message.Error = null;
        }
        catch (Exception ex)
        {
            message.Status = "Failed";
            message.RetryCount += 1;
            message.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TrySendSmsAsync(NotificationMessageEntity message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Channel, "Sms", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Receiver))
        {
            return;
        }

        try
        {
            var options = smsOptions.Value;
            if (!options.Enabled)
            {
                throw new InvalidOperationException("Sms channel disabled.");
            }
            if (string.IsNullOrWhiteSpace(options.ProviderUrl))
            {
                throw new InvalidOperationException("Sms provider url is required.");
            }

            var client = httpClientFactory.CreateClient(nameof(NotificationService));
            var body = JsonSerializer.Serialize(new
            {
                to = message.Receiver,
                sender = options.SenderId,
                title = message.Title,
                content = message.Content
            });
            using var response = await SendWithRetryAsync(
                ct => SendSmsRequestAsync(client, options, body, ct),
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                message.Status = "Sent";
                message.SentAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            else
            {
                message.Status = "Failed";
                message.RetryCount += 1;
                message.Error = $"Sms provider returned {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            message.Status = "Failed";
            message.RetryCount += 1;
            message.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sender,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var delay = TimeSpan.FromMilliseconds(300);
        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await sender(cancellationToken);
                if ((int)response.StatusCode >= 500 && attempt < maxAttempts)
                {
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken);
                    delay = delay * 2;
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                await Task.Delay(delay, cancellationToken);
                delay = delay * 2;
            }
        }

        throw lastException ?? new InvalidOperationException("HTTP send failed after retries.");
    }

    private static async Task<HttpResponseMessage> SendSmsRequestAsync(
        HttpClient client,
        SmsChannelOptions options,
        string body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.ProviderUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
        }

        return await client.SendAsync(request, cancellationToken);
    }
}

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
            var claimed = await dbContext.ScheduledJobs
                .Where(x => x.ScheduledJobId == candidate.ScheduledJobId && x.Status == "Pending")
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(x => x.Status, "Running"),
                    cancellationToken);
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
                if (string.Equals(job.JobType, "notification.webhook.retry", StringComparison.OrdinalIgnoreCase))
                {
                    var id = TryParseNotificationMessageId(job);
                    if (id.HasValue && messages.TryGetValue(id.Value, out var message))
                    {
                        await notificationService.TrySendWebhookAsync(message, cancellationToken);
                    }
                }
                else if (string.Equals(job.JobType, "notification.email.retry", StringComparison.OrdinalIgnoreCase))
                {
                    var id = TryParseNotificationMessageId(job);
                    if (id.HasValue && messages.TryGetValue(id.Value, out var message))
                    {
                        await notificationService.TrySendEmailAsync(message, cancellationToken);
                    }
                }
                else if (string.Equals(job.JobType, "notification.sms.retry", StringComparison.OrdinalIgnoreCase))
                {
                    var id = TryParseNotificationMessageId(job);
                    if (id.HasValue && messages.TryGetValue(id.Value, out var message))
                    {
                        await notificationService.TrySendSmsAsync(message, cancellationToken);
                    }
                }

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
