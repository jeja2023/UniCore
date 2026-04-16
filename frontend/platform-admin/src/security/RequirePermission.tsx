import React from "react";
import { Navigate } from "react-router-dom";
import { hasPermission, usePermissions } from "./permissions";

export function RequirePermission({
  permission,
  children,
}: {
  permission: string;
  children: React.ReactNode;
}) {
  const { loading, permissions } = usePermissions();
  if (loading) {
    return <div>权限加载中…</div>;
  }
  if (!hasPermission(permissions, permission)) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}

