using System.Collections.Concurrent;
using System.Text;

namespace Platform.AuditLog.Metrics;

public sealed class AuditExportMetricsStore
{
    private static readonly double[] DurationBucketsSeconds = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 20, 30, 60];
    private readonly ConcurrentDictionary<string, long> jobCount = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> jobDurationSum = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> jobDurationCount = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> jobDurationBucket = new(StringComparer.Ordinal);

    public void Record(string result, TimeSpan duration)
    {
        var labelKey = Escape(result);
        jobCount.AddOrUpdate(labelKey, 1, static (_, old) => old + 1);
        jobDurationSum.AddOrUpdate(labelKey, _ => duration.TotalSeconds, (_, old) => old + duration.TotalSeconds);
        jobDurationCount.AddOrUpdate(labelKey, 1, static (_, old) => old + 1);

        foreach (var le in DurationBucketsSeconds)
        {
            if (duration.TotalSeconds <= le)
            {
                var bucketKey = BuildBucketKey(result, le.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                jobDurationBucket.AddOrUpdate(bucketKey, 1, static (_, old) => old + 1);
            }
        }

        var infKey = BuildBucketKey(result, "+Inf");
        jobDurationBucket.AddOrUpdate(infKey, 1, static (_, old) => old + 1);
    }

    public void Increment(string result)
    {
        var labelKey = Escape(result);
        jobCount.AddOrUpdate(labelKey, 1, static (_, old) => old + 1);
    }

    public string ToPrometheusText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# HELP unicore_audit_export_jobs_total Audit export jobs processed.");
        sb.AppendLine("# TYPE unicore_audit_export_jobs_total counter");
        foreach (var item in jobCount.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_audit_export_jobs_total");
            sb.Append($"{{result=\"{Unescape(item.Key)}\"}} ");
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        sb.AppendLine("# HELP unicore_audit_export_job_duration_seconds Audit export job duration in seconds.");
        sb.AppendLine("# TYPE unicore_audit_export_job_duration_seconds histogram");
        foreach (var item in jobDurationBucket.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_audit_export_job_duration_seconds_bucket");
            sb.Append(ParseBucketKey(item.Key));
            sb.Append(' ');
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var item in jobDurationSum.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_audit_export_job_duration_seconds_sum");
            sb.Append($"{{result=\"{Unescape(item.Key)}\"}} ");
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var item in jobDurationCount.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append("unicore_audit_export_job_duration_seconds_count");
            sb.Append($"{{result=\"{Unescape(item.Key)}\"}} ");
            sb.AppendLine(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string BuildBucketKey(string result, string le) => $"{Escape(result)}|{Escape(le)}";

    private static string ParseBucketKey(string key)
    {
        var parts = key.Split('|');
        return $"{{result=\"{Unescape(parts[0])}\",le=\"{Unescape(parts[1])}\"}}";
    }

    private static string Escape(string input) => input.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    private static string Unescape(string input) => input.Replace("\\|", "|", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
}

