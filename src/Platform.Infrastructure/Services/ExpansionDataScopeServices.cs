namespace Platform.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Platform.Core.Common;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Text;
using System.Text.RegularExpressions;

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
