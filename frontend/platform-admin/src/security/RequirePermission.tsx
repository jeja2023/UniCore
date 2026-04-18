import React from "react";
import { Navigate } from "react-router-dom";
import { hasPermission, usePermissions } from "./permissions";
import { ROUTE_PATHS } from "../routes/routePaths";
import { PageAsyncState } from "../components/patterns/PageAsyncState";

export function RequirePermission({
  permission,
  children,
}: {
  permission: string;
  children: React.ReactNode;
}) {
  const { loading, permissions } = usePermissions();
  if (loading) {
    return <PageAsyncState loading={true} loadingText="权限加载中…" />;
  }
  if (!hasPermission(permissions, permission)) {
    return <Navigate to={ROUTE_PATHS.ROOT} replace />;
  }
  return <>{children}</>;
}

