using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Core.Abstractions;
using Platform.Core.Common;
using Platform.AuditLog.Metrics;
using Platform.WebApi.Auth;
using Platform.WebApi.Configuration;
using Platform.WebApi.Metrics;

namespace Platform.WebApi.Endpoints;

internal static class PlatformFoundationEndpoints
{
    internal static void MapPlatformFoundationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (HttpContext context) =>
            Results.Ok(AppResult<string>.Ok("ok", context.TraceIdentifier)));
        app.MapGet("/metrics", (IConfiguration configuration, RequestMetricsStore metricsStore, AuditExportMetricsStore exportMetrics, AuditLogWriteMetricsStore auditWriteMetrics) =>
        {
            if (!PlatformFeatureFlags.IsMetricsEnabled(configuration))
            {
                return Results.NotFound();
            }

            return Results.Text(
                metricsStore.ToPrometheusText() + exportMetrics.ToPrometheusText() + auditWriteMetrics.ToPrometheusText(),
                "text/plain; version=0.0.4");
        });
        app.MapHealthChecks("/api/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EndpointHelpers.WriteHealthResponseAsync
        });
        app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = EndpointHelpers.WriteHealthResponseAsync
        });
        app.MapGet("/api/platform/config/features", (IConfiguration configuration, HttpContext context) =>
        {
            var flags = configuration.GetSection("FeatureFlags").GetChildren()
                .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            return Results.Ok(AppResult<IReadOnlyDictionary<string, string>>.Ok(flags, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PlatformFeatureRead);
        app.MapPost("/api/platform/cache/{key}", async (string key, SetCacheRequest request, IAppCache cache, HttpContext context) =>
        {
            await cache.SetAsync(key, request.Value, request.TtlSeconds.HasValue ? TimeSpan.FromSeconds(request.TtlSeconds.Value) : null);
            return Results.Ok(AppResult<string>.Ok("cached", context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PlatformCacheWrite);
        app.MapGet("/api/platform/cache/{key}", async (string key, IAppCache cache, HttpContext context) =>
        {
            var value = await cache.GetAsync<string>(key);
            return Results.Ok(AppResult<string?>.Ok(value, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PlatformCacheRead);
        app.MapDelete("/api/platform/cache/{key}", async (string key, IAppCache cache, HttpContext context) =>
        {
            await cache.RemoveAsync(key);
            return Results.Ok(AppResult<string>.Ok("removed", context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PlatformCacheWrite);
        app.MapPost("/api/platform/files/upload", async (HttpRequest request, IFileStorage fileStorage, HttpContext context) =>
        {
            if (!request.HasFormContentType)
            {
                throw new AppException(ErrorCodes.ValidationError, "请使用 multipart/form-data 上传文件。");
            }

            var form = await request.ReadFormAsync();
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length <= 0)
            {
                throw new AppException(ErrorCodes.ValidationError, "未检测到有效文件。");
            }
            if (file.Length > 10 * 1024 * 1024)
            {
                throw new AppException(ErrorCodes.ValidationError, "文件大小不能超过 10 MB。");
            }

            await using var stream = file.OpenReadStream();
            var saved = await fileStorage.SaveAsync(stream, file.FileName, file.ContentType);
            return Results.Ok(AppResult<StoredFileInfo>.Ok(saved, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PlatformFileWrite);
        app.MapGet("/api/platform/files/{fileId}", async (string fileId, IFileStorage fileStorage) =>
        {
            var file = await fileStorage.ReadAsync(fileId);
            if (file is null)
            {
                throw new AppException(ErrorCodes.NotFound, "文件不存在。");
            }

            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequireAuthorization(PermissionPolicies.PlatformFileRead);
    }
}
