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
        if (!cancelled) setError(getErrorMessage(err, "加载模块契约失败"));
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
    const alignedFrontendModules = frontendModules.filter((module) => backendModulesByCode.has(module.moduleCode));
    const frontendOnlyModules = frontendModules.filter((module) => !backendModulesByCode.has(module.moduleCode));
    const backendOnlyModules = modules.filter(
      (module) => !frontendModules.some((frontendModule) => frontendModule.moduleCode === module.moduleCode)
    );

    const alignmentDetails: ModuleAlignmentDetail[] = alignedFrontendModules.map((frontendModule) => {
      const backendModule = backendModulesByCode.get(frontendModule.moduleCode);
      const backendPermissions = new Set(
        (backendModule?.permissions ?? []).map((permission) => permission.permissionCode ?? permission.code ?? "")
      );
      const frontendPermissions = new Set(frontendModule.routePermissions);
      const missingInBackend = Array.from(frontendPermissions).filter((permission) => !backendPermissions.has(permission));
      const backendPermissionsWithoutFrontendRoutes = Array.from(backendPermissions).filter(
        (permission) => permission && !frontendPermissions.has(permission)
      );

      const frontendRoutesByPath = new Map(frontendModule.routes.map((route) => [route.path, route]));
      const backendMenus = (backendModule?.menus ?? []).map((menu) => ({
        key: menu.menuCode ?? menu.key ?? menu.routePath ?? menu.path ?? "",
        path: menu.routePath ?? menu.path ?? "",
        permission: menu.permissionCode ?? menu.permission ?? "",
      }));

      const missingFrontendRoutesForMenus = backendMenus
        .map((menu) => {
          if (!menu.path || frontendRoutesByPath.has(menu.path)) {
            return null;
          }

          return `${menu.key} -> ${menu.path}`;
        })
        .filter((x): x is string => Boolean(x));

      const routePermissionMismatches = backendMenus
        .map((menu) => {
          if (!menu.path) {
            return null;
          }

          const frontendRoute = frontendRoutesByPath.get(menu.path);
          if (!frontendRoute) {
            return null;
          }

          const frontendPermission = frontendRoute.permission ?? "";
          const backendPermission = menu.permission ?? "";
          if (frontendPermission === backendPermission) {
            return null;
          }

          return `${menu.key} (${frontendPermission} != ${backendPermission})`;
        })
        .filter((x): x is string => Boolean(x));

      return {
        frontendModule,
        backendModule,
        missingInBackend,
        backendPermissionsWithoutFrontendRoutes,
        missingFrontendRoutesForMenus,
        routePermissionMismatches,
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
