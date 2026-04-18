import React from "react";
import { Route } from "react-router-dom";
import { RequirePermission } from "../security/RequirePermission";
import { PageAsyncState } from "../components/patterns/PageAsyncState";
import { lazyModuleRouteEntries, type ModuleRoute } from "./moduleRegistry.generated";

function normalizePath(path: string) {
  return path.startsWith("/") ? path.slice(1) : path;
}

export function renderModuleRoutes() {
  return lazyModuleRouteEntries.map((entry) => {
    const element = entry.permission ? (
      <RequirePermission permission={entry.permission}>
        <LazyModuleRoute loadRoutes={entry.loadRoutes} path={entry.path} />
      </RequirePermission>
    ) : (
      <LazyModuleRoute loadRoutes={entry.loadRoutes} path={entry.path} />
    );

    return (
      <Route
        key={`${entry.moduleCode}:${entry.path}`}
        path={normalizePath(entry.path)}
        element={element}
      />
    );
  });
}

type LazyModuleRouteProps = {
  path: string;
  loadRoutes: () => Promise<ReadonlyArray<ModuleRoute>>;
};

function LazyModuleRoute({ path, loadRoutes }: LazyModuleRouteProps) {
  const [element, setElement] = React.useState<React.ReactElement | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const routes = await loadRoutes();
        const matched = routes.find((route) => route.path === path);
        if (!matched) {
          throw new Error(`未找到模块路由：${path}`);
        }
        if (!cancelled) {
          setElement(matched.element);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "模块路由加载失败。");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [loadRoutes, path]);

  if (loading) {
    return <PageAsyncState loading={true} loadingText="模块加载中…" />;
  }

  if (error) {
    return <PageAsyncState loading={false} error={error} />;
  }

  return element;
}
