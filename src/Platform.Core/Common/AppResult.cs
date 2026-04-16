namespace Platform.Core.Common;

public sealed record AppResult<T>(bool Success, T? Data, string? ErrorCode, string? Message, string TraceId)
{
    public static AppResult<T> Ok(T data, string traceId) => new(true, data, null, null, traceId);

    public static AppResult<T> Fail(string errorCode, string message, string traceId) =>
        new(false, default, errorCode, message, traceId);
}
