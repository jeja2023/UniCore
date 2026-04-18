using System.Globalization;
using System.Text;

namespace Platform.AuditLog.Metrics;

public sealed class AuditLogWriteMetricsStore
{
    private long _enqueued;
    private long _dropped;
    private long _queueCompleted;
    private long _persistedRows;
    private long _batchFailures;

    public void RecordEnqueue() => Interlocked.Increment(ref _enqueued);

    public void RecordDropped() => Interlocked.Increment(ref _dropped);

    /// <summary>在后台批量处理结束时调用；成功时 <paramref name="persistedRows"/> 为写入行数。</summary>
    public void RecordBatchFinished(int batchSize, int persistedRows, bool success)
    {
        Interlocked.Add(ref _queueCompleted, batchSize);
        if (success)
        {
            Interlocked.Add(ref _persistedRows, persistedRows);
        }
        else
        {
            Interlocked.Increment(ref _batchFailures);
        }
    }

    public string ToPrometheusText()
    {
        var enqueued = Volatile.Read(ref _enqueued);
        var dropped = Volatile.Read(ref _dropped);
        var completed = Volatile.Read(ref _queueCompleted);
        var persisted = Volatile.Read(ref _persistedRows);
        var failures = Volatile.Read(ref _batchFailures);
        var inflight = Math.Max(0, enqueued - completed);

        var sb = new StringBuilder();
        sb.AppendLine("# HELP unicore_audit_write_enqueued_total Audit events accepted into the async write queue.");
        sb.AppendLine("# TYPE unicore_audit_write_enqueued_total counter");
        sb.Append("unicore_audit_write_enqueued_total ");
        sb.AppendLine(enqueued.ToString(CultureInfo.InvariantCulture));

        sb.AppendLine("# HELP unicore_audit_write_dropped_total Audit events discarded because the bounded write queue was full (DropWrite).");
        sb.AppendLine("# TYPE unicore_audit_write_dropped_total counter");
        sb.Append("unicore_audit_write_dropped_total ");
        sb.AppendLine(dropped.ToString(CultureInfo.InvariantCulture));

        sb.AppendLine("# HELP unicore_audit_write_persisted_rows_total Audit event rows persisted by the background writer.");
        sb.AppendLine("# TYPE unicore_audit_write_persisted_rows_total counter");
        sb.Append("unicore_audit_write_persisted_rows_total ");
        sb.AppendLine(persisted.ToString(CultureInfo.InvariantCulture));

        sb.AppendLine("# HELP unicore_audit_write_batch_failures_total Failed batches when persisting audit events.");
        sb.AppendLine("# TYPE unicore_audit_write_batch_failures_total counter");
        sb.Append("unicore_audit_write_batch_failures_total ");
        sb.AppendLine(failures.ToString(CultureInfo.InvariantCulture));

        sb.AppendLine("# HELP unicore_audit_write_queue_inflight Approximate backlog: enqueued minus batch-completed events.");
        sb.AppendLine("# TYPE unicore_audit_write_queue_inflight gauge");
        sb.Append("unicore_audit_write_queue_inflight ");
        sb.AppendLine(inflight.ToString(CultureInfo.InvariantCulture));

        return sb.ToString();
    }
}
