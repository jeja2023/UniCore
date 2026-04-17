import React from "react";
import { Route } from "react-router-dom";
import { RequirePermission } from "../security/RequirePermission";
import { moduleRoutes } from "./moduleRegistry.generated";

function normalizePath(path: string) {
  return path.startsWith("/") ? path.slice(1) : path;
}

export function renderModuleRoutes() {
  return moduleRoutes.map((route) => {
    const element = route.permission ? (
      <RequirePermission permission={route.permission}>
        {route.element}
      </RequirePermission>
    ) : (
      route.element
    );

    return (
      <Route
        key={route.path}
        path={normalizePath(route.path)}
        element={element}
      />
    );
  });
}
