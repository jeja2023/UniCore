namespace Platform.Core.Common;

public sealed class AppException(string errorCode, string message, int statusCode = 400) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int StatusCode { get; } = statusCode;
}
