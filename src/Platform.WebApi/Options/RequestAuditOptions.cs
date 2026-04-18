namespace Platform.WebApi.Options;

/// <summary>控制 HTTP 请求审计中间件写入的范围与采样。</summary>
public sealed class RequestAuditOptions
{
    public const string Section = "RequestAudit";

    /// <summary>为 false 时不记录 HTTP GET 请求（显著降低只读流量下的审计写入量）。默认 true，保持兼容。</summary>
    public bool AuditHttpGet { get; set; } = true;

    /// <summary>
    /// 对符合条件的请求按百分比采样写入：100 表示全量；0 表示不写 HTTP 请求审计（仍保留模块内显式审计）。
    /// </summary>
    public int SamplingPercent { get; set; } = 100;
}
