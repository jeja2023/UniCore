import React from "react";

import { routes as moduleRoutes0 } from "../../../modules/sample-module/routes";
import { menus as moduleMenus0 } from "../../../modules/sample-module/menu";
import { permissions as modulePermissions0 } from "../../../modules/sample-module/permissions";

export type ModuleRoute = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export type FrontendModuleMenu = {
  key: string;
  title: string;
  path: string;
  permission?: string | null;
};

export type FrontendModuleManifest = {
  sourceDir: string;
  packageName: string;
  version: string;
  moduleCode: string | null;
  routes: ReadonlyArray<ModuleRoute>;
  menus: ReadonlyArray<FrontendModuleMenu>;
  permissions: Record<string, string>;
};

function inferModuleCode(permissions: Record<string, string>): string | null {
  const values = Object.values(permissions ?? {}).filter(Boolean);
  const prefixes = Array.from(
    new Set(values.map((value) => String(value).split(".")[0]).filter(Boolean))
  );
  return prefixes.length === 1 ? prefixes[0] : null;
}

export const moduleRoutes: ModuleRoute[] = [
  ...moduleRoutes0,
];

export const frontendModules: FrontendModuleManifest[] = [
  {
    sourceDir: "sample-module",
    packageName: "@unicore/sample-module",
    version: "0.1.0",
    moduleCode: inferModuleCode(modulePermissions0),
    routes: moduleRoutes0,
    menus: moduleMenus0,
    permissions: modulePermissions0,
  },
];