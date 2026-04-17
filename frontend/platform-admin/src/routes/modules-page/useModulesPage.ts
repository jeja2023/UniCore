import { useEffect, useMemo, useState } from "react";
import { apiFetch } from "../../security/apiClient";
import { frontendModules } from "../moduleRegistry.generated";
import { getErrorMessage } from "../../utils/errorMessage";
import type { BackendModuleDto, ModuleAlignmentDetail, ModuleContractsResponse } from "./types";

export function useModulesPage() {
  const [modules, setModules] = useState<BackendModuleDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setError(null);
      setLoading(true);
      try {
        const resp = await apiFetch<ModuleContractsResponse>("/api/modules/contracts");
        if (!cancelled) setModules(resp.data);
      } catch (err: unknown) {
        if (!cancelled) setError(getErrorMessage(err, "加载失败"));
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
  }, []);

  const view = useMemo(() => {
    const backendModulesByCode = new Map(modules.map((module) => [module.moduleCode, module]));
    const alignedFrontendModules = frontendModules.filter(
      (module) => module.moduleCode && backendModulesByCode.has(module.moduleCode)
    );
    const frontendOnlyModules = frontendModules.filter(
      (module) => !module.moduleCode || !backendModulesByCode.has(module.moduleCode)
    );
    const backendOnlyModules = modules.filter(
      (module) => !frontendModules.some((frontendModule) => frontendModule.moduleCode === module.moduleCode)
    );

    const alignmentDetails: ModuleAlignmentDetail[] = alignedFrontendModules.map((frontendModule) => {
      const backendModule = backendModulesByCode.get(frontendModule.moduleCode!);
      const backendPermissions = new Set(
        (backendModule?.permissions ?? []).map((permission) => permission.permissionCode ?? permission.code ?? "")
      );
      const frontendPermissions = new Set(Object.values(frontendModule.permissions));
      const missingInBackend = Array.from(frontendPermissions).filter((permission) => !backendPermissions.has(permission));
      const missingInFrontend = Array.from(backendPermissions).filter(
        (permission) => permission && !frontendPermissions.has(permission)
      );

      const backendMenus = new Map(
        (backendModule?.menus ?? []).map((menu) => [
          menu.menuCode ?? menu.key ?? "",
          {
            path: menu.routePath ?? menu.path ?? "",
            permission: menu.permissionCode ?? menu.permission ?? "",
          },
        ])
      );

      const menuMismatches = frontendModule.menus
        .map((menu) => {
          const backendMenu = backendMenus.get(menu.key);
          if (!backendMenu) {
            return `缺少菜单 ${menu.key}`;
          }

          const issues: string[] = [];
          if (menu.path !== backendMenu.path) {
            issues.push(`path: ${menu.path} != ${backendMenu.path}`);
          }
          if ((menu.permission ?? "") !== (backendMenu.permission ?? "")) {
            issues.push(`permission: ${menu.permission ?? ""} != ${backendMenu.permission ?? ""}`);
          }
          return issues.length > 0 ? `${menu.key} (${issues.join("; ")})` : null;
        })
        .filter((x): x is string => Boolean(x));

      return {
        frontendModule,
        backendModule,
        missingInBackend,
        missingInFrontend,
        menuMismatches,
      };
    });

    return {
      alignmentDetails,
      frontendOnlyModules,
      backendOnlyModules,
    };
  }, [modules]);

  return {
    error,
    loading,
    alignmentDetails: view.alignmentDetails,
    frontendOnlyModules: view.frontendOnlyModules,
    backendOnlyModules: view.backendOnlyModules,
  };
}
