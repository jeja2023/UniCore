import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { apiFetch } from "./apiClient";
import { ROUTE_PATHS } from "../routes/routePaths";
import { AUTH_CHANGED_EVENT, clearAccessToken, getAccessToken } from "./tokenStore";

type MenuItem = { key: string; title: string; path: string; permission?: string | null };
type CurrentUserContextResponse = {
  data: {
    userId: string;
    username: string;
    displayName: string;
    tenantId: string;
    roles: string[];
    permissions: string[];
    menus: MenuItem[];
  };
};

type PermissionState = {
  loading: boolean;
  permissions: Set<string>;
  menus: MenuItem[];
  refresh: () => Promise<void>;
};

const PermissionContext = createContext<PermissionState | null>(null);

export function PermissionProvider({ children }: { children: React.ReactNode }) {
  const [loading, setLoading] = useState(true);
  const [permissions, setPermissions] = useState<Set<string>>(new Set());
  const [menus, setMenus] = useState<MenuItem[]>([]);

  async function refresh() {
    const token = getAccessToken();
    if (!token) {
      setPermissions(new Set());
      setMenus([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const resp = await apiFetch<CurrentUserContextResponse>("/api/me/context");
      setPermissions(new Set(resp.data.permissions ?? []));
      setMenus(resp.data.menus ?? []);
    } catch (error) {
      const status =
        typeof error === "object" && error !== null && "status" in error
          ? (error as { status?: unknown }).status
          : undefined;
      setPermissions(new Set());
      setMenus([]);
      if (status === 401) {
        clearAccessToken();
        if (window.location.pathname !== ROUTE_PATHS.LOGIN) {
          window.location.replace(ROUTE_PATHS.LOGIN);
        }
      }
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    const handleAuthChanged = () => {
      void refresh();
    };
    window.addEventListener(AUTH_CHANGED_EVENT, handleAuthChanged);
    return () => {
      window.removeEventListener(AUTH_CHANGED_EVENT, handleAuthChanged);
    };
  }, []);

  const value = useMemo<PermissionState>(
    () => ({ loading, permissions, menus, refresh }),
    [loading, permissions, menus]
  );

  return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
}

export function usePermissions() {
  const ctx = useContext(PermissionContext);
  if (!ctx) throw new Error("usePermissions must be used within PermissionProvider");
  return ctx;
}

export function hasPermission(set: Set<string>, required?: string | null) {
  if (!required) return true;
  return set.has(required);
}

export function Can({
  permission,
  children,
  fallback = null,
}: {
  permission?: string | null;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  const { permissions } = usePermissions();
  if (!hasPermission(permissions, permission)) return <>{fallback}</>;
  return <>{children}</>;
}

