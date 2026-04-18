import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { PageSection } from "../components/patterns/PageSection";
import { PageAsyncState } from "../components/patterns/PageAsyncState";
import { useTheme } from "../design/theme/ThemeProvider";
import { apiFetch, type ApiError } from "../security/apiClient";
import { Button } from "../components/base/Button";
import { createGlassCheckboxVars, createGlassControlVars } from "../styles/glass";

type AppResult<T> = {
  success?: boolean;
  data: T;
  message?: string | null;
  traceId?: string;
};

type AuditExportJobDto = {
  jobId: string;
  createdBy: string;
  status: string;
  createdAt: string;
  completedAt?: string | null;
  error?: string | null;
  retryCount: number;
  maxRetries: number;
  nextAttemptAt?: string | null;
  lastAttemptAt?: string | null;
  deadLettered: boolean;
};

type AuditExportJobPageDto = {
  items: AuditExportJobDto[];
  total: number;
  page: number;
  pageSize: number;
};

const STATUS_OPTIONS_FALLBACK = ["Pending", "Processing", "Completed", "Retry", "Failed", "DeadLettered", "Discarded"] as const;
const VISIBLE_COLUMNS_STORAGE_KEY = "platform-admin:audit-exports:visible-columns";
const OPTIONAL_COLUMN_KEYS = [
  "retry",
  "nextAttemptAt",
  "lastAttemptAt",
  "createdAt",
  "completedAt",
  "error",
] as const;
type OptionalColumnKey = (typeof OPTIONAL_COLUMN_KEYS)[number];
type ColumnKey = "jobId" | "status" | OptionalColumnKey | "actions";
type SortBy = "createdAt" | "completedAt" | "status";
type SortDir = "asc" | "desc";
type BatchAction = "replay" | "discard";

function readVisibleColumns(): Record<OptionalColumnKey, boolean> {
  const defaults: Record<OptionalColumnKey, boolean> = {
    retry: true,
    nextAttemptAt: true,
    lastAttemptAt: true,
    createdAt: true,
    completedAt: true,
    error: true,
  };
  if (typeof window === "undefined") {
    return defaults;
  }
  try {
    const raw = window.localStorage.getItem(VISIBLE_COLUMNS_STORAGE_KEY);
    if (!raw) {
      return defaults;
    }
    const parsed = JSON.parse(raw) as Partial<Record<OptionalColumnKey, unknown>>;
    return {
      retry: typeof parsed.retry === "boolean" ? parsed.retry : defaults.retry,
      nextAttemptAt: typeof parsed.nextAttemptAt === "boolean" ? parsed.nextAttemptAt : defaults.nextAttemptAt,
      lastAttemptAt: typeof parsed.lastAttemptAt === "boolean" ? parsed.lastAttemptAt : defaults.lastAttemptAt,
      createdAt: typeof parsed.createdAt === "boolean" ? parsed.createdAt : defaults.createdAt,
      completedAt: typeof parsed.completedAt === "boolean" ? parsed.completedAt : defaults.completedAt,
      error: typeof parsed.error === "boolean" ? parsed.error : defaults.error,
    };
  } catch {
    return defaults;
  }
}

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

function formatDate(value?: string | null) {
  if (!value) return "-";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString("zh-CN");
}

function auditExportStatusDisplayLabel(status: string) {
  const k = status.trim().toLowerCase();
  const map: Record<string, string> = {
    pending: "等待中",
    processing: "处理中",
    running: "处理中",
    completed: "已完成",
    retry: "重试",
    failed: "失败",
    deadlettered: "死信",
    discarded: "已丢弃",
  };
  return map[k] ?? status;
}

function formatError(err: unknown) {
  if (typeof err === "object" && err !== null && "message" in err) {
    return String((err as ApiError).message);
  }
  return "加载失败";
}

function buildQuery(params: Record<string, string | undefined>) {
  const parts = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v!)}`);
  return parts.length ? `?${parts.join("&")}` : "";
}

function normalizeStatus(status: string) {
  return status.trim().toLowerCase();
}

function StatusBadge({ status }: { status: string }) {
  const { tokens } = useTheme();
  const s = normalizeStatus(status);
  const palette =
    s === "completed"
      ? { bg: tokens.colors.bgSubtle, fg: tokens.colors.brand, border: tokens.colors.border }
      : s === "processing" || s === "running"
        ? { bg: tokens.colors.bgSubtle, fg: tokens.colors.text, border: tokens.colors.border }
        : s === "retry" || s === "pending"
          ? { bg: tokens.colors.bgSubtle, fg: tokens.colors.textSecondary, border: tokens.colors.border }
          : s === "deadlettered" || s.includes("dead")
            ? { bg: "rgba(255, 0, 0, 0.06)", fg: tokens.colors.danger, border: "rgba(255, 0, 0, 0.2)" }
            : s === "failed" || s === "discarded"
              ? { bg: "rgba(255, 0, 0, 0.06)", fg: tokens.colors.danger, border: "rgba(255, 0, 0, 0.2)" }
              : { bg: tokens.colors.bgSubtle, fg: tokens.colors.textSecondary, border: tokens.colors.border };

  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "2px 8px",
        borderRadius: 999,
        fontSize: 12,
        border: `1px solid ${palette.border}`,
        background: palette.bg,
        color: palette.fg,
        whiteSpace: "nowrap",
      }}
    >
      {auditExportStatusDisplayLabel(status)}
    </span>
  );
}

function Drawer({
  title,
  open,
  onClose,
  children,
}: {
  title: string;
  open: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  const { tokens } = useTheme();
  if (!open) return null;
  return (
    <div
      onClick={onClose}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.35)",
        display: "flex",
        justifyContent: "flex-end",
        zIndex: 50,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: "min(760px, 100vw)",
          height: "100vh",
          overflow: "auto",
          boxShadow: "-8px 0 24px rgba(0,0,0,0.2)",
          border: `1px solid ${tokens.colors.border}`,
          background: tokens.colors.bg,
          padding: tokens.space.lg,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <div style={{ fontWeight: 800 }}>{title}</div>
          <Button onClick={onClose}>关闭</Button>
        </div>
        <div style={{ marginTop: tokens.space.md }}>{children}</div>
      </div>
    </div>
  );
}

function ConfirmDialog({
  open,
  title,
  content,
  confirmText,
  confirmVariant = "primary",
  onCancel,
  onConfirm,
  confirming = false,
}: {
  open: boolean;
  title: string;
  content: string;
  confirmText: string;
  confirmVariant?: "primary" | "danger";
  onCancel: () => void;
  onConfirm: () => void;
  confirming?: boolean;
}) {
  const { tokens } = useTheme();
  if (!open) return null;
  return (
    <div
      onClick={onCancel}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.35)",
        display: "grid",
        placeItems: "center",
        zIndex: 60,
        padding: tokens.space.lg,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: "min(520px, 100%)",
          border: `1px solid ${tokens.colors.border}`,
          borderRadius: tokens.radius.md,
          background: tokens.colors.bg,
          padding: tokens.space.lg,
        }}
      >
        <div style={{ fontWeight: 800 }}>{title}</div>
        <div style={{ marginTop: 8, fontSize: 13, color: tokens.colors.textSecondary }}>{content}</div>
        <div style={{ marginTop: tokens.space.lg, display: "flex", justifyContent: "flex-end", gap: 8 }}>
          <Button onClick={onCancel} disabled={confirming}>
            取消
          </Button>
          <Button variant={confirmVariant} onClick={onConfirm} disabled={confirming}>
            {confirming ? "处理中…" : confirmText}
          </Button>
        </div>
      </div>
    </div>
  );
}

export function AuditExportsPage() {
  const { tokens } = useTheme();
  const glassControlStyle = createGlassControlVars(tokens);
  const glassCheckboxStyle = createGlassCheckboxVars(tokens);
  const location = useLocation();
  const navigate = useNavigate();
  const initializedTabRef = useRef(false);
  const initialQuery = useMemo(() => {
    const params = new URLSearchParams(location.search);
    const tabValue = params.get("tab");
    const parsedPage = Number.parseInt(params.get("page") ?? "1", 10);
    const pageValue = Number.isFinite(parsedPage) && parsedPage > 0 ? parsedPage : 1;
    const status = params.get("status")?.trim() ?? "";
    const jobId = params.get("jobId")?.trim() ?? "";
    const from = params.get("from")?.trim() ?? "";
    const to = params.get("to")?.trim() ?? "";
    const sortByRaw = params.get("sortBy");
    const sortDirRaw = params.get("sortDir");
    const sortBy: SortBy = sortByRaw === "status" || sortByRaw === "completedAt" ? sortByRaw : "createdAt";
    const sortDir: SortDir = sortDirRaw === "asc" ? "asc" : "desc";
    const autoRefreshRaw = params.get("autoRefresh");
    const autoRefresh = autoRefreshRaw === null ? true : autoRefreshRaw !== "0";
    const tab: "all" | "dlq" = tabValue === "dlq" ? "dlq" : "all";
    return {
      tab,
      page: pageValue,
      filters: { status, jobId, from, to },
      sortBy,
      sortDir,
      autoRefresh,
    };
  }, [location.search]);
  const [tab, setTab] = useState<"all" | "dlq">(initialQuery.tab);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(initialQuery.page);
  const [pageSize] = useState(20);
  const [data, setData] = useState<AuditExportJobPageDto | null>(null);
  const [actingJobId, setActingJobId] = useState<string | null>(null);
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detail, setDetail] = useState<AuditExportJobDto | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(initialQuery.autoRefresh);
  const [statusOptions, setStatusOptions] = useState<string[]>([...STATUS_OPTIONS_FALLBACK]);
  const [lastAppliedAt, setLastAppliedAt] = useState<number | null>(null);
  const [sortBy, setSortBy] = useState<SortBy>(initialQuery.sortBy);
  const [sortDir, setSortDir] = useState<SortDir>(initialQuery.sortDir);
  const [showColumnSettings, setShowColumnSettings] = useState(false);
  const [visibleColumns, setVisibleColumns] = useState<Record<OptionalColumnKey, boolean>>(() => readVisibleColumns());
  const [pendingBatchAction, setPendingBatchAction] = useState<BatchAction | null>(null);
  const [toast, setToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [lastBatchResult, setLastBatchResult] = useState<{
    action: BatchAction;
    successCount: number;
    failedCount: number;
    failedJobIds: string[];
    occurredAt: string;
  } | null>(null);

  const [draftFilters, setDraftFilters] = useState<{
    status: string;
    jobId: string;
    from: string;
    to: string;
  }>(initialQuery.filters);
  const [appliedFilters, setAppliedFilters] = useState<{
    status: string;
    jobId: string;
    from: string;
    to: string;
  }>(initialQuery.filters);

  const title = useMemo(() => (tab === "dlq" ? "审计导出任务（死信队列（DLQ））" : "审计导出任务"), [tab]);
  const trimmedJobId = draftFilters.jobId.trim();
  const isJobIdValid = trimmedJobId.length === 0 || isGuid(trimmedJobId);
  const hasPendingFilterChanges = useMemo(
    () =>
      draftFilters.status !== appliedFilters.status ||
      draftFilters.jobId !== appliedFilters.jobId ||
      draftFilters.from !== appliedFilters.from ||
      draftFilters.to !== appliedFilters.to,
    [appliedFilters.from, appliedFilters.jobId, appliedFilters.status, appliedFilters.to, draftFilters.from, draftFilters.jobId, draftFilters.status, draftFilters.to]
  );
  const columnVisibilityMap = useMemo<Record<ColumnKey, boolean>>(
    () => ({
      jobId: true,
      status: true,
      retry: visibleColumns.retry,
      nextAttemptAt: visibleColumns.nextAttemptAt,
      lastAttemptAt: visibleColumns.lastAttemptAt,
      createdAt: visibleColumns.createdAt,
      completedAt: visibleColumns.completedAt,
      error: visibleColumns.error,
      actions: true,
    }),
    [visibleColumns]
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const base = tab === "dlq" ? "/api/audit/exports/dlq" : "/api/audit/exports";
      const query = buildQuery({
        page: String(page),
        pageSize: String(pageSize),
        status: tab === "dlq" ? undefined : appliedFilters.status.trim() || undefined,
        jobId: isGuid(appliedFilters.jobId.trim()) ? appliedFilters.jobId.trim() : undefined,
        from: appliedFilters.from || undefined,
        to: appliedFilters.to || undefined,
        sortBy,
        sortDir,
      });
      const path = `${base}${query}`;
      const resp = await apiFetch<AppResult<AuditExportJobPageDto>>(path);
      setData(resp.data);
    } catch (e) {
      setError(formatError(e));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [appliedFilters.from, appliedFilters.jobId, appliedFilters.status, appliedFilters.to, page, pageSize, sortBy, sortDir, tab]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!initializedTabRef.current) {
      initializedTabRef.current = true;
      return;
    }
    setPage(1);
    setSelected({});
  }, [tab]);

  useEffect(() => {
    const params = new URLSearchParams();
    if (tab === "dlq") params.set("tab", "dlq");
    if (page > 1) params.set("page", String(page));
    if (appliedFilters.status) params.set("status", appliedFilters.status);
    if (appliedFilters.jobId) params.set("jobId", appliedFilters.jobId);
    if (appliedFilters.from) params.set("from", appliedFilters.from);
    if (appliedFilters.to) params.set("to", appliedFilters.to);
    if (sortBy !== "createdAt") params.set("sortBy", sortBy);
    if (sortDir !== "desc") params.set("sortDir", sortDir);
    if (!autoRefresh) params.set("autoRefresh", "0");
    const nextSearch = params.toString();
    const currentSearch = location.search.startsWith("?") ? location.search.slice(1) : location.search;
    if (nextSearch !== currentSearch) {
      navigate({ search: nextSearch ? `?${nextSearch}` : "" }, { replace: true });
    }
  }, [appliedFilters.from, appliedFilters.jobId, appliedFilters.status, appliedFilters.to, autoRefresh, location.search, navigate, page, sortBy, sortDir, tab]);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const resp = await apiFetch<AppResult<string[]>>("/api/audit/exports/statuses");
        const normalized = (resp.data ?? []).map((x) => x.trim()).filter(Boolean);
        if (active && normalized.length > 0) {
          setStatusOptions(Array.from(new Set(normalized)));
        }
      } catch {
        // 若接口请求失败，保留默认状态列表。
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!autoRefresh || detailOpen) {
      return;
    }
    const timer = window.setInterval(() => {
      void load();
    }, 10_000);
    return () => window.clearInterval(timer);
  }, [autoRefresh, detailOpen, load]);

  useEffect(() => {
    if (lastAppliedAt === null) {
      return;
    }
    const timer = window.setTimeout(() => setLastAppliedAt(null), 1200);
    return () => window.clearTimeout(timer);
  }, [lastAppliedAt]);

  useEffect(() => {
    try {
      window.localStorage.setItem(VISIBLE_COLUMNS_STORAGE_KEY, JSON.stringify(visibleColumns));
    } catch {
      // 忽略本地存储写入失败。
    }
  }, [visibleColumns]);

  const applyFilters = useCallback(() => {
    if (!isJobIdValid || loading) {
      return;
    }
    setAppliedFilters(draftFilters);
    setPage(1);
    setLastAppliedAt(Date.now());
  }, [draftFilters, isJobIdValid, loading]);

  const toggleSort = useCallback((nextSortBy: SortBy) => {
    setPage(1);
    setSortBy((prevSortBy) => {
      if (prevSortBy !== nextSortBy) {
        setSortDir("desc");
        return nextSortBy;
      }
      setSortDir((prevDir) => (prevDir === "desc" ? "asc" : "desc"));
      return prevSortBy;
    });
  }, []);

  const getSortIndicator = useCallback(
    (column: SortBy) => (sortBy === column ? (sortDir === "desc" ? " ↓" : " ↑") : ""),
    [sortBy, sortDir]
  );

  async function replay(jobId: string) {
    setActingJobId(jobId);
    setError(null);
    try {
      await apiFetch<AppResult<AuditExportJobDto>>(`/api/audit/exports/${jobId}/dlq/replay`, { method: "POST" });
      await load();
    } catch (e) {
      setError(formatError(e));
    } finally {
      setActingJobId(null);
    }
  }

  async function discard(jobId: string) {
    setActingJobId(jobId);
    setError(null);
    try {
      await apiFetch<AppResult<AuditExportJobDto>>(`/api/audit/exports/${jobId}/dlq/discard`, { method: "POST" });
      await load();
    } catch (e) {
      setError(formatError(e));
    } finally {
      setActingJobId(null);
    }
  }

  async function openDetail(jobId: string) {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetailError(null);
    setDetail(null);
    try {
      const resp = await apiFetch<AppResult<AuditExportJobDto>>(`/api/audit/exports/${jobId}`);
      setDetail(resp.data);
    } catch (e) {
      setDetailError(formatError(e));
    } finally {
      setDetailLoading(false);
    }
  }

  const items = useMemo(() => data?.items ?? [], [data]);
  const dlqItems = useMemo(
    () => items.filter((x) => x.deadLettered || normalizeStatus(x.status).includes("dead")),
    [items]
  );
  const visibleSelectedCount = useMemo(
    () => dlqItems.filter((x) => selected[x.jobId]).length,
    [dlqItems, selected]
  );

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  function exportBatchResult(result: {
    action: BatchAction;
    successCount: number;
    failedCount: number;
    failedJobIds: string[];
    occurredAt: string;
  }) {
    const header = "action,occurredAt,successCount,failedCount,failedJobIds";
    const row = [
      result.action,
      result.occurredAt,
      String(result.successCount),
      String(result.failedCount),
      `"${result.failedJobIds.join("|").replaceAll("\"", "\"\"")}"`,
    ].join(",");
    const csv = `${header}\n${row}\n`;
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `audit-batch-${result.action}-${new Date().toISOString().replaceAll(":", "").slice(0, 15)}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  async function retryFailedItems() {
    if (!lastBatchResult || lastBatchResult.failedJobIds.length === 0) {
      return;
    }
    if (lastBatchResult.action === "replay") {
      setSelected(Object.fromEntries(lastBatchResult.failedJobIds.map((id) => [id, true])));
      await batchReplay(lastBatchResult.failedJobIds);
      return;
    }
    setSelected(Object.fromEntries(lastBatchResult.failedJobIds.map((id) => [id, true])));
    await batchDiscard(lastBatchResult.failedJobIds);
  }

  async function batchReplay(overrideTargets?: string[]) {
    const targets = overrideTargets ?? dlqItems.filter((x) => selected[x.jobId]).map((x) => x.jobId);
    if (targets.length === 0) return;
    setError(null);
    setActingJobId("__batch__");
    try {
      const settled = await Promise.allSettled(
        targets.map((id) => apiFetch<AppResult<AuditExportJobDto>>(`/api/audit/exports/${id}/dlq/replay`, { method: "POST" }))
      );
      const successCount = settled.filter((x) => x.status === "fulfilled").length;
      const failedCount = settled.length - successCount;
      const failedJobIds = settled
        .map((item, idx) => (item.status === "rejected" ? targets[idx] : null))
        .filter((x): x is string => Boolean(x));
      setSelected({});
      await load();
      setLastBatchResult({
        action: "replay",
        successCount,
        failedCount,
        failedJobIds,
        occurredAt: new Date().toISOString(),
      });
      setToast({
        kind: failedCount > 0 ? "error" : "success",
        message: failedCount > 0 ? `批量重放完成：成功 ${successCount}，失败 ${failedCount}` : `批量重放成功：${successCount} 条`,
      });
    } catch (e) {
      setError(formatError(e));
      setToast({ kind: "error", message: `批量重放失败：${formatError(e)}` });
    } finally {
      setActingJobId(null);
      setPendingBatchAction(null);
    }
  }

  async function batchDiscard(overrideTargets?: string[]) {
    const targets = overrideTargets ?? dlqItems.filter((x) => selected[x.jobId]).map((x) => x.jobId);
    if (targets.length === 0) return;
    setError(null);
    setActingJobId("__batch__");
    try {
      const settled = await Promise.allSettled(
        targets.map((id) => apiFetch<AppResult<AuditExportJobDto>>(`/api/audit/exports/${id}/dlq/discard`, { method: "POST" }))
      );
      const successCount = settled.filter((x) => x.status === "fulfilled").length;
      const failedCount = settled.length - successCount;
      const failedJobIds = settled
        .map((item, idx) => (item.status === "rejected" ? targets[idx] : null))
        .filter((x): x is string => Boolean(x));
      setSelected({});
      await load();
      setLastBatchResult({
        action: "discard",
        successCount,
        failedCount,
        failedJobIds,
        occurredAt: new Date().toISOString(),
      });
      setToast({
        kind: failedCount > 0 ? "error" : "success",
        message: failedCount > 0 ? `批量丢弃完成：成功 ${successCount}，失败 ${failedCount}` : `批量丢弃成功：${successCount} 条`,
      });
    } catch (e) {
      setError(formatError(e));
      setToast({ kind: "error", message: `批量丢弃失败：${formatError(e)}` });
    } finally {
      setActingJobId(null);
      setPendingBatchAction(null);
    }
  }

  function download(jobId: string) {
    window.open(`/api/audit/exports/${jobId}/download`, "_blank", "noopener,noreferrer");
  }

  return (
    <div style={{ display: "grid", gap: tokens.space.lg }}>
      <PageSection title={title}>
        <div style={{ display: "grid", gap: tokens.space.md }}>
          <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <Button variant={tab === "all" ? "primary" : "default"} onClick={() => setTab("all")}>
            全部
          </Button>
          <Button variant={tab === "dlq" ? "primary" : "default"} onClick={() => setTab("dlq")}>
            死信队列（DLQ）
          </Button>
            <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
              <Button onClick={() => setShowColumnSettings((v) => !v)}>{showColumnSettings ? "收起列设置" : "列设置"}</Button>
              <label
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 6,
                  fontSize: 12,
                  color: tokens.colors.textSecondary,
                }}
              >
                <input
                  type="checkbox"
                  checked={autoRefresh}
                  onChange={(e) => setAutoRefresh(e.target.checked)}
                  disabled={loading}
                  className="glass-checkbox"
                  style={glassCheckboxStyle}
                />
                自动刷新（10s）
              </label>
              <Button disabled={loading} onClick={() => void load()}>
                刷新
              </Button>
            </div>
          </div>
          {showColumnSettings && (
            <div
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: 12,
                padding: "10px 12px",
                border: `1px solid ${tokens.colors.border}`,
                borderRadius: tokens.radius.sm,
                background: tokens.colors.bgSubtle,
              }}
            >
              {OPTIONAL_COLUMN_KEYS.map((key) => (
                <label key={key} style={{ display: "inline-flex", alignItems: "center", gap: 6, fontSize: 12 }}>
                  <input
                    type="checkbox"
                    checked={visibleColumns[key]}
                    onChange={(e) => setVisibleColumns((prev) => ({ ...prev, [key]: e.target.checked }))}
                    className="glass-checkbox"
                    style={glassCheckboxStyle}
                  />
                  {{
                    retry: "重试次数",
                    nextAttemptAt: "下次尝试时间",
                    lastAttemptAt: "上次尝试时间",
                    createdAt: "创建时间",
                    completedAt: "完成时间",
                    error: "错误",
                  }[key]}
                </label>
              ))}
            </div>
          )}

          <div style={{ display: "grid", gap: 10, gridTemplateColumns: "repeat(12, minmax(0, 1fr))", alignItems: "end" }}>
            {tab === "all" && (
              <label style={{ gridColumn: "span 3", fontSize: 12, color: tokens.colors.textSecondary }}>
                状态
                <select
                  value={draftFilters.status}
                  onChange={(e) => setDraftFilters((s) => ({ ...s, status: e.target.value }))}
                  className="glass-control"
                  style={{
                    ...glassControlStyle,
                    marginTop: 6,
                    width: "100%",
                    padding: "8px 10px",
                    color: tokens.colors.text,
                  }}
                >
                  <option value="">全部状态</option>
                  {statusOptions.map((status) => (
                    <option key={status} value={status}>
                      {auditExportStatusDisplayLabel(status)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label style={{ gridColumn: tab === "all" ? "span 3" : "span 4", fontSize: 12, color: tokens.colors.textSecondary }}>
              任务标识（ID，服务端精确匹配）
              <input
                value={draftFilters.jobId}
                onChange={(e) => setDraftFilters((s) => ({ ...s, jobId: e.target.value }))}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    applyFilters();
                  }
                }}
                placeholder="输入完整全局唯一标识（GUID）"
                className="glass-control"
                style={{
                  ...glassControlStyle,
                  marginTop: 6,
                  width: "100%",
                  padding: "8px 10px",
                  border: `1px solid ${isJobIdValid ? tokens.glass.border : tokens.colors.danger}`,
                  color: tokens.colors.text,
                }}
              />
              {!isJobIdValid && (
                <div style={{ marginTop: 6, fontSize: 12, color: tokens.colors.danger }}>
                  请输入合法全局唯一标识（GUID），例如 xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                </div>
              )}
            </label>
            <label style={{ gridColumn: "span 2", fontSize: 12, color: tokens.colors.textSecondary }}>
              开始日期
              <input
                type="date"
                value={draftFilters.from}
                onChange={(e) => setDraftFilters((s) => ({ ...s, from: e.target.value }))}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    applyFilters();
                  }
                }}
                className="glass-control"
                style={{
                  ...glassControlStyle,
                  marginTop: 6,
                  width: "100%",
                  padding: "8px 10px",
                  color: tokens.colors.text,
                }}
              />
            </label>
            <label style={{ gridColumn: "span 2", fontSize: 12, color: tokens.colors.textSecondary }}>
              结束日期
              <input
                type="date"
                value={draftFilters.to}
                onChange={(e) => setDraftFilters((s) => ({ ...s, to: e.target.value }))}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    applyFilters();
                  }
                }}
                className="glass-control"
                style={{
                  ...glassControlStyle,
                  marginTop: 6,
                  width: "100%",
                  padding: "8px 10px",
                  color: tokens.colors.text,
                }}
              />
            </label>
            <div style={{ gridColumn: "span 2", display: "flex", gap: 8, justifyContent: "flex-end" }}>
              {hasPendingFilterChanges && (
                <div style={{ alignSelf: "center", fontSize: 12, color: tokens.colors.textSecondary }}>有未应用筛选</div>
              )}
              {!hasPendingFilterChanges && lastAppliedAt !== null && (
                <div style={{ alignSelf: "center", fontSize: 12, color: tokens.colors.brand }}>已应用</div>
              )}
              <Button
                disabled={loading}
                onClick={() => {
                  const empty = { status: "", jobId: "", from: "", to: "" };
                  setDraftFilters(empty);
                  setAppliedFilters(empty);
                  setPage(1);
                }}
              >
                重置
              </Button>
              <Button
                variant={hasPendingFilterChanges ? "primary" : "default"}
                disabled={loading || !isJobIdValid}
                onClick={applyFilters}
              >
                应用
              </Button>
            </div>
          </div>

          {tab === "dlq" && (
            <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
              <div style={{ fontSize: 12, color: tokens.colors.textSecondary }}>
                已选 {visibleSelectedCount} / 当前列表 {dlqItems.length}
              </div>
              <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
                {lastBatchResult && (
                  <Button onClick={() => exportBatchResult(lastBatchResult)}>导出结果</Button>
                )}
                {lastBatchResult && lastBatchResult.failedCount > 0 && (
                  <Button variant="primary" disabled={actingJobId === "__batch__"} onClick={() => void retryFailedItems()}>
                    仅重试失败项（{lastBatchResult.failedCount}）
                  </Button>
                )}
                <Button
                  variant="primary"
                  disabled={actingJobId === "__batch__" || visibleSelectedCount === 0}
                  onClick={() => setPendingBatchAction("replay")}
                >
                  批量重放
                </Button>
                <Button
                  variant="danger"
                  disabled={actingJobId === "__batch__" || visibleSelectedCount === 0}
                  onClick={() => setPendingBatchAction("discard")}
                >
                  批量丢弃
                </Button>
              </div>
            </div>
          )}
        </div>
      </PageSection>

      <PageAsyncState loading={loading} error={error} loadingText="加载导出任务…" />

      {!loading && !error && (
        <PageSection title="任务列表">
          {items.length === 0 ? (
            <div style={{ color: tokens.colors.textSecondary }}>暂无任务</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr>
                    {[
                      tab === "dlq" ? "选择" : null,
                      columnVisibilityMap.jobId ? "任务标识（ID）" : null,
                      columnVisibilityMap.status ? `状态${getSortIndicator("status")}` : null,
                      columnVisibilityMap.retry ? "重试次数" : null,
                      columnVisibilityMap.nextAttemptAt ? "下次尝试时间" : null,
                      columnVisibilityMap.lastAttemptAt ? "上次尝试时间" : null,
                      columnVisibilityMap.createdAt ? `创建时间${getSortIndicator("createdAt")}` : null,
                      columnVisibilityMap.completedAt ? `完成时间${getSortIndicator("completedAt")}` : null,
                      columnVisibilityMap.error ? "错误" : null,
                      columnVisibilityMap.actions ? "操作" : null,
                    ]
                      .filter((x): x is string => Boolean(x))
                      .map((h) => (
                      <th
                        key={h}
                        onClick={() => {
                          if (h.startsWith("状态")) {
                            toggleSort("status");
                            return;
                          }
                          if (h.startsWith("创建时间")) {
                            toggleSort("createdAt");
                            return;
                          }
                          if (h.startsWith("完成时间")) {
                            toggleSort("completedAt");
                          }
                        }}
                        style={{
                          textAlign: "left",
                          fontSize: 12,
                          color: tokens.colors.textSecondary,
                          border: `1px solid ${tokens.colors.border}`,
                          padding: "8px 6px",
                          whiteSpace: "nowrap",
                          cursor:
                            h.startsWith("状态") || h.startsWith("创建时间") || h.startsWith("完成时间")
                              ? "pointer"
                              : "default",
                          userSelect: "none",
                        }}
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {items.map((job) => {
                    const canDlqAct = job.deadLettered || tab === "dlq" || job.status.toLowerCase().includes("dead");
                    const normalized = normalizeStatus(job.status);
                    const isCompleted = normalized === "completed";
                    const canDownload = isCompleted && !job.deadLettered;
                    return (
                      <tr key={job.jobId}>
                        {tab === "dlq" && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            <input
                              type="checkbox"
                              checked={Boolean(selected[job.jobId])}
                              onChange={(e) => setSelected((s) => ({ ...s, [job.jobId]: e.target.checked }))}
                              className="glass-checkbox"
                              style={glassCheckboxStyle}
                            />
                          </td>
                        )}
                        {columnVisibilityMap.jobId && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                              <code style={{ fontSize: 12 }}>{job.jobId}</code>
                              <Button
                                onClick={() => {
                                  void navigator.clipboard?.writeText(job.jobId).catch(() => undefined);
                                }}
                              >
                                复制
                              </Button>
                              <Button onClick={() => void openDetail(job.jobId)}>详情</Button>
                            </div>
                          </td>
                        )}
                        {columnVisibilityMap.status && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            <StatusBadge status={job.status} />
                          </td>
                        )}
                        {columnVisibilityMap.retry && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            {job.retryCount}/{job.maxRetries}
                          </td>
                        )}
                        {columnVisibilityMap.nextAttemptAt && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            {formatDate(job.nextAttemptAt)}
                          </td>
                        )}
                        {columnVisibilityMap.lastAttemptAt && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            {formatDate(job.lastAttemptAt)}
                          </td>
                        )}
                        {columnVisibilityMap.createdAt && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            {formatDate(job.createdAt)}
                          </td>
                        )}
                        {columnVisibilityMap.completedAt && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                            {formatDate(job.completedAt)}
                          </td>
                        )}
                        {columnVisibilityMap.error && (
                          <td
                            style={{
                              padding: "8px 6px",
                              border: `1px solid ${tokens.colors.border}`,
                              maxWidth: 380,
                            }}
                          >
                            <div
                              title={job.error ?? ""}
                              style={{
                                fontSize: 12,
                                color: tokens.colors.textSecondary,
                                overflow: "hidden",
                                textOverflow: "ellipsis",
                                whiteSpace: "nowrap",
                              }}
                            >
                              {job.error ?? "-"}
                            </div>
                          </td>
                        )}
                        {columnVisibilityMap.actions && (
                          <td style={{ padding: "8px 6px", border: `1px solid ${tokens.colors.border}` }}>
                          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                            <Button disabled={!canDownload} onClick={() => download(job.jobId)}>
                              下载
                            </Button>
                            {canDlqAct ? (
                              <>
                                <Button
                                  variant="primary"
                                  disabled={actingJobId === job.jobId}
                                  onClick={() => void replay(job.jobId)}
                                >
                                  重放
                                </Button>
                                <Button
                                  variant="danger"
                                  disabled={actingJobId === job.jobId}
                                  onClick={() => void discard(job.jobId)}
                                >
                                  丢弃
                                </Button>
                              </>
                            ) : (
                              <span style={{ color: tokens.colors.textSecondary, fontSize: 12, padding: "8px 0" }}>-</span>
                            )}
                          </div>
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {data && (
            <div style={{ display: "flex", gap: 8, marginTop: tokens.space.md, alignItems: "center" }}>
              <Button disabled={page <= 1 || loading} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                上一页
              </Button>
              <Button
                disabled={loading || (data.page * data.pageSize >= data.total)}
                onClick={() => setPage((p) => p + 1)}
              >
                下一页
              </Button>
              <div style={{ fontSize: 12, color: tokens.colors.textSecondary }}>
                第 {data.page} 页 / 共 {Math.max(1, Math.ceil(data.total / data.pageSize))} 页（{data.total} 条）
              </div>
            </div>
          )}
        </PageSection>
      )}

      <Drawer
        title={detail ? `任务详情：${detail.jobId}` : "任务详情"}
        open={detailOpen}
        onClose={() => {
          setDetailOpen(false);
          setDetail(null);
          setDetailError(null);
        }}
      >
        <PageAsyncState loading={detailLoading} error={detailError} loadingText="加载详情…" />
        {detail && (
          <div style={{ display: "grid", gap: tokens.space.md }}>
            <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
              <StatusBadge status={detail.status} />
              <div style={{ fontSize: 12, color: tokens.colors.textSecondary }}>
                重试 {detail.retryCount}/{detail.maxRetries}
              </div>
              <div style={{ marginLeft: "auto", display: "flex", gap: 8, flexWrap: "wrap" }}>
                <Button disabled={normalizeStatus(detail.status) !== "completed"} onClick={() => download(detail.jobId)}>
                  下载
                </Button>
                {(detail.deadLettered || normalizeStatus(detail.status).includes("dead")) && (
                  <>
                    <Button variant="primary" disabled={actingJobId === detail.jobId} onClick={() => void replay(detail.jobId)}>
                      重放
                    </Button>
                    <Button variant="danger" disabled={actingJobId === detail.jobId} onClick={() => void discard(detail.jobId)}>
                      丢弃
                    </Button>
                  </>
                )}
              </div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "180px 1fr", rowGap: 8, columnGap: 12, fontSize: 12 }}>
              {[
                ["任务标识（ID）", detail.jobId],
                ["创建人", detail.createdBy],
                ["创建时间", formatDate(detail.createdAt)],
                ["完成时间", formatDate(detail.completedAt)],
                ["下次尝试时间", formatDate(detail.nextAttemptAt)],
                ["上次尝试时间", formatDate(detail.lastAttemptAt)],
                ["已进入死信", String(detail.deadLettered)],
              ].map(([k, v]) => (
                <React.Fragment key={k}>
                  <div style={{ color: tokens.colors.textSecondary, paddingTop: 6 }}>{k}</div>
                  <div className="u-display-field u-display-field--compact" style={{ fontWeight: 500 }}>
                    {v}
                  </div>
                </React.Fragment>
              ))}
            </div>

            <div>
              <div className="u-display-field__title">错误信息</div>
              <pre className="u-display-field u-display-field--code" style={{ margin: 0 }}>
                {detail.error ?? "-"}
              </pre>
            </div>

            <div>
              <div className="u-display-field__title">原始数据（JSON）</div>
              <pre className="u-display-field u-display-field--code" style={{ margin: 0 }}>
                {JSON.stringify(detail, null, 2)}
              </pre>
            </div>
          </div>
        )}
      </Drawer>

      <ConfirmDialog
        open={pendingBatchAction === "replay"}
        title="确认批量重放"
        content={`将对当前已选 ${visibleSelectedCount} 条死信任务执行重放。`}
        confirmText="确认重放"
        confirmVariant="primary"
        confirming={actingJobId === "__batch__"}
        onCancel={() => setPendingBatchAction(null)}
        onConfirm={() => void batchReplay()}
      />
      <ConfirmDialog
        open={pendingBatchAction === "discard"}
        title="确认批量丢弃"
        content={`将对当前已选 ${visibleSelectedCount} 条死信任务执行丢弃，且不可恢复。`}
        confirmText="确认丢弃"
        confirmVariant="danger"
        confirming={actingJobId === "__batch__"}
        onCancel={() => setPendingBatchAction(null)}
        onConfirm={() => void batchDiscard()}
      />
      {toast && (
        <div
          style={{
            position: "fixed",
            right: 20,
            bottom: 20,
            zIndex: 70,
            maxWidth: 420,
            padding: "10px 12px",
            borderRadius: tokens.radius.sm,
            border: `1px solid ${toast.kind === "error" ? "rgba(255,0,0,0.3)" : tokens.colors.border}`,
            background: toast.kind === "error" ? "rgba(255,0,0,0.08)" : tokens.colors.bg,
            color: toast.kind === "error" ? tokens.colors.danger : tokens.colors.text,
            boxShadow: "0 4px 12px rgba(0,0,0,0.12)",
            fontSize: 13,
          }}
        >
          {toast.message}
        </div>
      )}
    </div>
  );
}

