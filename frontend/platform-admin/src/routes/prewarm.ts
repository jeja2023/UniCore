import { lazyModuleRouteEntries } from "./moduleRegistry.generated";

type ScheduleOptions = {
  timeoutMs?: number;
};

function scheduleIdle(task: () => void, options: ScheduleOptions = {}) {
  const timeoutMs = options.timeoutMs ?? 1200;
  if (typeof window === "undefined") return;

  const w = window as unknown as {
    requestIdleCallback?: (cb: () => void, opts?: { timeout: number }) => number;
  };

  if (typeof w.requestIdleCallback === "function") {
    w.requestIdleCallback(task, { timeout: timeoutMs });
    return;
  }

  window.setTimeout(task, 0);
}

function safeVoid(promise: Promise<unknown>) {
  void promise.catch(() => undefined);
}

let lastPrewarmKey: string | null = null;

/**
 * 登录后首屏“预热”：
 * - 预取高频页面 chunk（React.lazy 对应的动态 import）
 * - 预取高频模块 routes（模块契约的 loadRoutes）
 *
 * 设计目标：不阻塞渲染，不抛错，不重复执行。
 */
export function prewarmAfterAuth(params: {
  /** 用于去重。建议传 accessToken 或其 hash。 */
  key: string;
  /** 菜单顺序一般即“高频优先”的候选来源。 */
  menuPaths?: ReadonlyArray<string>;
  /** 最多预热多少个模块入口 */
  maxModuleCount?: number;
}) {
  if (typeof window === "undefined") return;
  if (!params.key) return;
  if (lastPrewarmKey === params.key) return;
  lastPrewarmKey = params.key;

  scheduleIdle(() => {
    // 平台内置页面：按常用页面做首屏预取（纯预取 chunk，不触发渲染）
    safeVoid(import("./HomePage"));
    safeVoid(import("./UsersPage"));
    safeVoid(import("./AuditExportsPage"));
    safeVoid(import("./ModulesPage"));
    safeVoid(import("./DataScopeGovernancePage"));
  });

  scheduleIdle(() => {
    const menuPaths = new Set(params.menuPaths ?? []);
    const maxCount = params.maxModuleCount ?? 3;

    const candidates = lazyModuleRouteEntries
      .filter((entry) => (menuPaths.size ? menuPaths.has(entry.path) : true))
      .slice(0, maxCount);

    for (const entry of candidates) {
      safeVoid(entry.loadRoutes());
    }
  });
}

