using System.Security.Claims;
using System.Text;
using Platform.AuditLog.Services;
using Platform.Core.Common;
using Platform.Identity.Services;
using Platform.Module.Abstractions.Contracts;
using Platform.WebApi.Auth;

namespace Platform.WebApi.Endpoints;

internal static class AuditAndModuleEndpoints
{
    internal static void MapAuditAndModuleEndpoints(this WebApplication app, IReadOnlyCollection<IBusinessModule> discoveredModules)
    {
        app.MapPost("/api/audit/events", async (WriteAuditRequest request, AuditLogService auditLogService, HttpContext context) =>
        {
            await auditLogService.WriteAsync(new AuditEvent(
                request.EventCode,
                request.Description,
                request.Actor,
                DateTimeOffset.UtcNow,
                request.Level,
                request.RequestPath,
                request.HttpMethod,
                request.StatusCode,
                request.TraceId));
            return Results.Ok(AppResult<string>.Ok("created", context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapGet("/api/audit/events", async (AuditLogService auditLogService, HttpContext context) =>
        {
            var filter = EndpointHelpers.BuildAuditFilter(context.Request.Query);
            var data = await auditLogService.QueryPagedAsync(filter);
            return Results.Ok(AppResult<AuditQueryResult>.Ok(data, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapGet("/api/audit/events/export", async (AuditLogService auditLogService, HttpContext context) =>
        {
            var filter = EndpointHelpers.BuildAuditFilter(context.Request.Query);
            var rows = await auditLogService.QueryAsync(filter);
            var fields = AuditCsvBuilder.ParseFields(context.Request.Query["fields"]);
            var csv = AuditCsvBuilder.Build(rows, fields);
            return Results.File(
                Encoding.UTF8.GetBytes(csv),
                "text/csv; charset=utf-8",
                $"audit-events-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapPost("/api/audit/exports", async (CreateAuditExportRequest request, AuditExportService auditExportService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            var filter = request.Filter ?? new AuditQueryFilter();
            var fields = request.Fields ?? [];
            var callbackUrl = EndpointHelpers.ParseHttpUri(request.CallbackUrl);
            var downloadUrl = callbackUrl is null
                ? null
                : $"{context.Request.Scheme}://{context.Request.Host}/api/audit/exports/{{jobId}}/download";
            var job = await auditExportService.CreateJobAsync(requester, filter, fields, callbackUrl, downloadUrl);
            return Results.Ok(AppResult<AuditExportJobDto>.Ok(EndpointHelpers.ToAuditExportJobDto(job), context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapGet("/api/audit/exports", async (AuditExportService auditExportService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            var query = context.Request.Query;
            var filter = new AuditExportJobQueryFilter(
                Status: query["status"],
                From: EndpointHelpers.ParseDate(query["from"]),
                To: EndpointHelpers.ParseDate(query["to"]),
                Page: EndpointHelpers.ParseInt(query["page"]),
                PageSize: EndpointHelpers.ParseInt(query["pageSize"]));
            var result = await auditExportService.QueryJobsAsync(requester, filter);
            return Results.Ok(AppResult<AuditExportJobPageDto>.Ok(EndpointHelpers.ToAuditExportJobPageDto(result), context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapGet("/api/audit/exports/{jobId}", async (string jobId, AuditExportService auditExportService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            if (!Guid.TryParse(jobId, out var id))
            {
                throw new AppException(ErrorCodes.NotFound, "导出任务不存在。");
            }

            var job = await auditExportService.GetJobAsync(id, requester);
            if (job is null)
            {
                throw new AppException(ErrorCodes.NotFound, "导出任务不存在。");
            }

            return Results.Ok(AppResult<AuditExportJobDto>.Ok(EndpointHelpers.ToAuditExportJobDto(job), context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapGet("/api/audit/exports/{jobId}/download", async (string jobId, AuditExportService auditExportService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            if (!Guid.TryParse(jobId, out var id))
            {
                throw new AppException(ErrorCodes.NotFound, "导出任务不存在。");
            }

            var csv = await auditExportService.GetCompletedCsvAsync(id, requester);
            if (string.IsNullOrEmpty(csv))
            {
                throw new AppException(ErrorCodes.ValidationError, "导出任务尚未完成。");
            }

            return Results.File(
                Encoding.UTF8.GetBytes(csv),
                "text/csv; charset=utf-8",
                $"audit-export-{id:N}.csv");
        }).RequireAuthorization(PermissionPolicies.AuditRead);

        app.MapGet("/api/me/context", async (CurrentUserContextService currentUserContextService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            if (!Guid.TryParse(requester, out var requesterId))
            {
                throw new AppException(ErrorCodes.ValidationError, "当前登录用户标识无效。");
            }

            var roles = context.User.Claims
                .Where(x => x.Type == ClaimTypes.Role)
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            var snapshot = await currentUserContextService.BuildSnapshotAsync(requesterId, roles, context.RequestAborted);
            if (snapshot is null)
            {
                throw new AppException(ErrorCodes.Unauthorized, "登录用户不存在。", StatusCodes.Status401Unauthorized);
            }

            var permissionSet = snapshot.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var menus = EndpointHelpers.BuildPlatformMenus()
                .Concat(discoveredModules.SelectMany(module => module.GetMenus()
                    .Select(menu => new MenuItemDto(menu.MenuCode, menu.Title, menu.RoutePath, menu.PermissionCode))))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Where(x => string.IsNullOrWhiteSpace(x.Permission) || permissionSet.Contains(x.Permission))
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var payload = new CurrentUserContextDto(
                snapshot.UserId.ToString(),
                snapshot.Username,
                snapshot.DisplayName,
                snapshot.TenantId,
                snapshot.Roles,
                snapshot.Permissions,
                menus);
            return Results.Ok(AppResult<CurrentUserContextDto>.Ok(payload, context.TraceIdentifier));
        }).RequireAuthorization();

        app.MapGet("/api/modules/contracts", (HttpContext context) =>
        {
            var payload = discoveredModules.Select(x => new
            {
                x.Metadata.ModuleCode,
                x.Metadata.ModuleName,
                x.Metadata.ModuleVersion,
                Permissions = x.GetPermissions(),
                Menus = x.GetMenus(),
                Audits = x.GetAuditDeclarations()
            });

            return Results.Ok(AppResult<object>.Ok(payload, context.TraceIdentifier));
        }).RequireAuthorization();

        app.MapPost("/api/modules/contracts/validate", (ValidateModuleContractsRequest? request, HttpContext context) =>
        {
            var protocolVersion = request?.ProtocolVersion;
            var result = EndpointHelpers.BuildModuleContractValidationResult(discoveredModules, protocolVersion);
            return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/modules/contracts/report", (string? protocolVersion, string? format, HttpContext context) =>
        {
            var report = EndpointHelpers.BuildModuleContractValidationResult(discoveredModules, protocolVersion);
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = EndpointHelpers.BuildModuleContractValidationCsv(report);
                return Results.File(
                    Encoding.UTF8.GetBytes(csv),
                    "text/csv; charset=utf-8",
                    $"module-contract-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            return Results.Ok(AppResult<object>.Ok(report, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/modules/contracts/report/download", (string? protocolVersion) =>
        {
            var report = EndpointHelpers.BuildModuleContractValidationResult(discoveredModules, protocolVersion);
            var csv = EndpointHelpers.BuildModuleContractValidationCsv(report);
            return Results.File(
                Encoding.UTF8.GetBytes(csv),
                "text/csv; charset=utf-8",
                $"module-contract-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }).RequireAuthorization(PermissionPolicies.PermissionRead);
    }
}
