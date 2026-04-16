using Platform.AuditLog.Services;
using System.Security.Claims;

namespace Platform.WebApi.Middleware;

public sealed class RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AuditLogService auditLogService)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var shouldAudit = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/api/audit/events", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

        await next(context);

        if (!shouldAudit)
        {
            return;
        }

        try
        {
            var actor = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.Identity?.Name
                ?? "anonymous";
            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? "Error" : statusCode >= 400 ? "Warning" : "Info";
            var eventCode = $"http.{method.ToLowerInvariant()}";
            var description = $"{method} {path}";

            await auditLogService.WriteAsync(new AuditEvent(
                eventCode,
                description,
                actor,
                DateTimeOffset.UtcNow,
                level,
                RequestPath: path,
                HttpMethod: method,
                StatusCode: statusCode,
                TraceId: context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write request audit log for path {Path}", path);
        }
    }
}
