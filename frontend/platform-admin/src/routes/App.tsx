import React from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { ShellLayout } from "./ShellLayout";
import { RequireAuth } from "../security/RequireAuth";
import { RequirePermission } from "../security/RequirePermission";
import { privateAppRoutes, publicAppRoutes } from "./appRoutes";
import { ROUTE_PATHS } from "./routePaths";
import { renderModuleRoutes } from "./moduleRegistry";
import { PageAsyncState } from "../components/patterns/PageAsyncState";

export function App() {
  const wrapWithSuspense = (element: React.ReactElement) => (
    <React.Suspense fallback={<PageAsyncState loading={true} loadingText="页面加载中..." />}>
      {element}
    </React.Suspense>
  );

  return (
    <Routes>
      {publicAppRoutes.map((route) => (
        <Route
          key={route.key}
          path={route.path}
          element={wrapWithSuspense(route.element)}
        />
      ))}

      <Route
        path="/"
        element={
          <RequireAuth>
            <ShellLayout />
          </RequireAuth>
        }
      >
        {privateAppRoutes.map((route) => {
          const element = route.permission ? (
            <RequirePermission permission={route.permission}>
              {wrapWithSuspense(route.element)}
            </RequirePermission>
          ) : (
            wrapWithSuspense(route.element)
          );

          if (route.index) {
            return <Route key={route.key} index element={element} />;
          }

          return <Route key={route.key} path={route.path} element={element} />;
        })}
        {renderModuleRoutes()}
      </Route>

      <Route path="*" element={<Navigate to={ROUTE_PATHS.ROOT} replace />} />
    </Routes>
  );
}

