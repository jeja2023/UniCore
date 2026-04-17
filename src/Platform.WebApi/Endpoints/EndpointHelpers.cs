using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.AuditLog.Services;
using Platform.Core.Common;
using Platform.Module.Abstractions.Contracts;
using Platform.WebApi.Auth;

namespace Platform.WebApi.Endpoints;

internal static class EndpointHelpers
{
    internal static AuditQueryFilter BuildAuditFilter(IQueryCollection query) =>
        new(
            From: ParseDate(query["from"]),
            To: ParseDate(query["to"]),
            RequestPath: query["requestPath"],
            HttpMethod: query["httpMethod"],
            StatusCode: ParseInt(query["statusCode"]),
            Actor: query["actor"],
            EventCode: query["eventCode"],
            Level: query["level"],
            TraceId: query["traceId"],
            Limit: ParseInt(query["limit"]),
            Page: ParseInt(query["page"]),
            PageSize: ParseInt(query["pageSize"]),
            Sort: query["sort"]);

    internal static DateTimeOffset? ParseDate(string? input) =>
        DateTimeOffset.TryParse(input, out var value) ? value : null;

    internal static int? ParseInt(string? input) =>
        int.TryParse(input, out var value) ? value : null;

    internal static Uri? ParseHttpUri(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AppException(ErrorCodes.ValidationError, "callbackUrl 必须是合法的 http/https 绝对地址。");
        }

        return uri;
    }

    internal static string GetRequester(HttpContext? context)
    {
        var requester = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(requester))
        {
            throw new AppException(ErrorCodes.Unauthorized, "未识别到登录用户。", StatusCodes.Status401Unauthorized);
        }

        return requester;
    }

    internal static string GetOptionalRequester(HttpContext? context)
    {
        var requester = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(requester) ? "system" : requester;
    }

    internal static MenuItemDto[] BuildPlatformMenus() =>
        PermissionCatalog.GetPlatformMenus()
            .Select(x => new MenuItemDto(x.Key, x.Title, x.Path, x.PermissionCode))
            .ToArray();

    internal static AuditExportJobDto ToAuditExportJobDto(AuditExportJobInfo job) =>
        new(job.JobId, job.CreatedBy, job.Status, job.CreatedAt, job.CompletedAt, job.Error);

    internal static AuditExportJobPageDto ToAuditExportJobPageDto(AuditExportJobPageResult page) =>
        new(page.Items.Select(ToAuditExportJobDto).ToArray(), page.Total, page.Page, page.PageSize);

    internal static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                x => x.Key,
                x => new
                {
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    durationMs = x.Value.Duration.TotalMilliseconds
                })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    internal static object BuildModuleContractValidationResult(IReadOnlyCollection<IBusinessModule> modules, string? protocolVersion)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var seenModuleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenMenuCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var moduleResults = new List<object>();
        var validAuditLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

        foreach (var module in modules.OrderBy(x => x.Metadata.ModuleCode, StringComparer.OrdinalIgnoreCase))
        {
            var metadata = module.Metadata;
            var moduleErrors = new List<string>();
            var moduleWarnings = new List<string>();
            var moduleCode = metadata.ModuleCode?.Trim() ?? string.Empty;
            var moduleVersion = metadata.ModuleVersion?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(moduleCode))
            {
                moduleErrors.Add("moduleCode 不能为空。");
            }
            else if (!Regex.IsMatch(moduleCode, "^[a-z][a-z0-9_-]{1,63}$"))
            {
                moduleErrors.Add($"moduleCode 格式不符合约定: {moduleCode}");
            }

            if (!string.IsNullOrWhiteSpace(moduleCode) && !seenModuleCodes.Add(moduleCode))
            {
                moduleErrors.Add($"moduleCode 重复: {moduleCode}");
            }

            if (string.IsNullOrWhiteSpace(moduleVersion))
            {
                moduleErrors.Add("moduleVersion 不能为空。");
            }
            else if (!Regex.IsMatch(moduleVersion, "^\\d+\\.\\d+\\.\\d+(-[0-9A-Za-z.-]+)?$"))
            {
                moduleErrors.Add($"moduleVersion 非语义化版本: {moduleVersion}");
            }

            if (!string.IsNullOrWhiteSpace(protocolVersion) &&
                TryGetSemVerMajor(moduleVersion, out var moduleMajor) &&
                TryGetSemVerMajor(protocolVersion, out var protocolMajor) &&
                moduleMajor != protocolMajor)
            {
                moduleWarnings.Add($"模块主版本({moduleMajor})与协议主版本({protocolMajor})不一致。");
            }

            var permissions = module.GetPermissions();
            var permissionCodes = permissions.Select(x => x.PermissionCode).Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var duplicatePermissionCodes = permissions
                .GroupBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            if (duplicatePermissionCodes.Length > 0)
            {
                moduleErrors.Add($"权限点重复: {string.Join(", ", duplicatePermissionCodes)}");
            }

            foreach (var menu in module.GetMenus())
            {
                if (string.IsNullOrWhiteSpace(menu.MenuCode))
                {
                    moduleErrors.Add("存在 menuCode 为空的菜单声明。");
                    continue;
                }
                if (!seenMenuCodes.Add(menu.MenuCode))
                {
                    moduleErrors.Add($"menuCode 重复: {menu.MenuCode}");
                }
                if (string.IsNullOrWhiteSpace(menu.PermissionCode))
                {
                    moduleWarnings.Add($"菜单 {menu.MenuCode} 未声明 permissionCode。");
                }
                else if (!permissionCodes.Contains(menu.PermissionCode))
                {
                    moduleWarnings.Add($"菜单 {menu.MenuCode} 绑定了未声明权限点: {menu.PermissionCode}");
                }
            }

            foreach (var audit in module.GetAuditDeclarations())
            {
                if (!validAuditLevels.Contains(audit.Level))
                {
                    moduleWarnings.Add($"审计事件 {audit.EventCode} 使用了非常规 level: {audit.Level}");
                }
            }

            errors.AddRange(moduleErrors.Select(x => $"[{moduleCode}] {x}"));
            warnings.AddRange(moduleWarnings.Select(x => $"[{moduleCode}] {x}"));
            moduleResults.Add(new
            {
                metadata.ModuleCode,
                metadata.ModuleName,
                metadata.ModuleVersion,
                IsValid = moduleErrors.Count == 0,
                ErrorCount = moduleErrors.Count,
                WarningCount = moduleWarnings.Count,
                Errors = moduleErrors,
                Warnings = moduleWarnings
            });
        }

        return new
        {
            IsValid = errors.Count == 0,
            ModuleCount = moduleResults.Count,
            ErrorCount = errors.Count,
            WarningCount = warnings.Count,
            Errors = errors,
            Warnings = warnings,
            Modules = moduleResults
        };
    }

    internal static string BuildModuleContractValidationCsv(object report)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(report));
        var root = doc.RootElement;
        var modulesElement = root.TryGetProperty("modules", out var camelModules)
            ? camelModules
            : root.GetProperty("Modules");
        var lines = new List<string>
        {
            "\"moduleCode\",\"moduleName\",\"moduleVersion\",\"isValid\",\"errorCount\",\"warningCount\",\"errors\",\"warnings\""
        };

        foreach (var module in modulesElement.EnumerateArray())
        {
            var errors = string.Join(" | ", GetPropertyCaseInsensitive(module, "errors").EnumerateArray().Select(x => x.GetString() ?? string.Empty));
            var warnings = string.Join(" | ", GetPropertyCaseInsensitive(module, "warnings").EnumerateArray().Select(x => x.GetString() ?? string.Empty));
            lines.Add(string.Join(",",
                EscapeCsv(GetPropertyCaseInsensitive(module, "moduleCode").GetString()),
                EscapeCsv(GetPropertyCaseInsensitive(module, "moduleName").GetString()),
                EscapeCsv(GetPropertyCaseInsensitive(module, "moduleVersion").GetString()),
                EscapeCsv(GetPropertyCaseInsensitive(module, "isValid").GetBoolean().ToString()),
                EscapeCsv(GetPropertyCaseInsensitive(module, "errorCount").GetInt32().ToString()),
                EscapeCsv(GetPropertyCaseInsensitive(module, "warningCount").GetInt32().ToString()),
                EscapeCsv(errors),
                EscapeCsv(warnings)));
        }

        return string.Join("\n", lines);
    }

    private static bool TryGetSemVerMajor(string version, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var input = version.Trim();
        var separator = input.IndexOfAny(['.', '-']);
        var majorText = separator >= 0 ? input[..separator] : input;
        return int.TryParse(majorText, out major);
    }

    private static string EscapeCsv(string? input)
    {
        var value = input ?? string.Empty;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static JsonElement GetPropertyCaseInsensitive(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var value))
        {
            return value;
        }

        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (obj.TryGetProperty(pascal, out value))
        {
            return value;
        }

        throw new KeyNotFoundException($"Property not found: {name}");
    }
}
