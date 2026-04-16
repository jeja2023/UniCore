using Platform.Core.Common;
using System.Text.Json;

namespace Platform.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, IHostEnvironment environment, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteErrorAsync(context, ex.StatusCode, ex.ErrorCode, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            var message = environment.IsDevelopment() ? "系统内部错误（开发模式可查看日志）" : "系统内部错误";
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, message);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var payload = AppResult<object>.Fail(errorCode, message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
