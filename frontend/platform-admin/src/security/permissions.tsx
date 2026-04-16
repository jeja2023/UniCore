import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { apiFetch } from "./apiClient";

type ModuleContractsResponse = {
  data: Array<{
    moduleCode: string;
    moduleName: string;
    moduleVersion: string;
    permissions: Array<{ code: string; name: string }>;
    menus: Array<{
      key: string;
      title: string;
      path: string;
      permission?: string | null;
    }>;
  }>;
};

type MenuItem = { key: string; title: string; path: string; permission?: string | null };

const builtInMenus: MenuItem[] = [
  { key: "platform.home", title: "首页", path: "/" },
  { key: "platform.users", title: "用户", path: "/identity/users", permission: "user.read" },
  { key: "platform.data-scope", title: "数据权限治理", path: "/permission/data-scope", permission: "permission.read" },
  { key: "platform.modules", title: "模块", path: "/modules" },
];

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
    setLoading(true);
    try {
      const resp = await apiFetch<ModuleContractsResponse>("/api/modules/contracts");
      const perm = new Set<string>();
      const contractMenus = resp.data.flatMap((m) => m.menus ?? []);
      for (const m of resp.data) {
        for (const p of m.permissions ?? []) {
          if (p?.code) perm.add(p.code);
        }
      }
      const menu = [...builtInMenus, ...contractMenus].filter((item, index, all) => {
        return all.findIndex((x) => x.key === item.key || x.path === item.path) === index;
      });
      setPermissions(perm);
      setMenus(menu);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
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

