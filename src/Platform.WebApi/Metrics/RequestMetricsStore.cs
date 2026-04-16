using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Platform.WebApi.Metrics;

public sealed class RequestMetricsStore
{
    private static readonly double[] DurationBucketsSeconds = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];
    private readonly ConcurrentDictionary<string, long> requestCount = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> requestDurationSum = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> requestDurationCount = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> requestDurationBucket = new(StringComparer.Ordinal);

    public void Record(string method, string path, int statusCode, TimeSpan duration)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path;
        var labelKey = BuildLabelKey(method, normalizedPath, statusCode);
        requestCount.AddOrUpdate(labelKey, 1, static (_, old) => old + 1);
        requestDurationSum.AddOrUpdate(
            labelKey,
            _ => duration.TotalSeconds,
            (_, old) => old + duration.TotalSeconds);
        requestDurationCount.AddOrUpdate(labelKey, 1, static (_, old) => old + 1);

        foreach (var le in DurationBucketsSeconds)
        {
            if (duration.TotalSeconds <= le)
            {
                var bucketKey = BuildBucketKey(method, normalizedPath, statusCode, le.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                requestDurationBucket.AddOrUpdate(bucketKey, 1, static (_, old) => old + 1);
            }
        }

        var infKey = BuildBucketKey(method, normalizedPath, statusCode, "+Inf");
        requestDurationBucket.AddOrUpdate(infKey, 1, static (_, old) => old + 1);
    }

    public string ToPrometheusText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# HELP unicore_http_requests_total Total HTTP requests.");
        sb.AppendLine("# TYPE unicore_http_requests_total counter");
        foreach (var item in requestCount.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_http_requests_total");
            sb.Append(ParseLabelKey(item.Key));
            sb.Append(' ');
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        sb.AppendLine("# HELP unicore_http_request_duration_seconds HTTP request duration in seconds.");
        sb.AppendLine("# TYPE unicore_http_request_duration_seconds histogram");
        foreach (var item in requestDurationBucket.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_http_request_duration_seconds_bucket");
            sb.Append(ParseBucketKey(item.Key));
            sb.Append(' ');
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var item in requestDurationSum.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_http_request_duration_seconds_sum");
            sb.Append(ParseLabelKey(item.Key));
            sb.Append(' ');
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var item in requestDurationCount.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_http_request_duration_seconds_count");
            sb.Append(ParseLabelKey(item.Key));
            sb.Append(' ');
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string BuildLabelKey(string method, string path, int statusCode) =>
        $"{Escape(method)}|{Escape(path)}|{statusCode}";

    private static string BuildBucketKey(string method, string path, int statusCode, string le) =>
        $"{Escape(method)}|{Escape(path)}|{statusCode}|{Escape(le)}";

    private static string ParseLabelKey(string key)
    {
        var parts = key.Split('|');
        return $"{{method=\"{Unescape(parts[0])}\",path=\"{Unescape(parts[1])}\",status=\"{Unescape(parts[2])}\"}}";
    }

    private static string ParseBucketKey(string key)
    {
        var parts = key.Split('|');
        return $"{{method=\"{Unescape(parts[0])}\",path=\"{Unescape(parts[1])}\",status=\"{Unescape(parts[2])}\",le=\"{Unescape(parts[3])}\"}}";
    }

    private static string Escape(string input) => input.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    private static string Unescape(string input) => input.Replace("\\|", "|", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
}

public sealed class RequestMetricsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestMetricsStore metricsStore)
    {
        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();
        metricsStore.Record(
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.Response.StatusCode,
            sw.Elapsed);
    }
}
