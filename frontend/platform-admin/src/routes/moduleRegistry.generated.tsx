import React from "react";

export type ModuleRoute = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export type FrontendModuleManifest = {
  sourceDir: string;
  packageName: string;
  version: string;
  moduleCode: string;
  routes: ReadonlyArray<Pick<ModuleRoute, "path" | "permission">>;
  routePaths: ReadonlyArray<string>;
  routePermissions: ReadonlyArray<string>;
};

export type LazyModuleRouteEntry = {
  moduleCode: string;
  path: string;
  permission?: string | null;
  loadRoutes: () => Promise<ReadonlyArray<ModuleRoute>>;
};

function collectRoutePaths(paths: ReadonlyArray<string>): string[] {
  return Array.from(new Set(paths.filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function collectRoutePermissions(permissions: ReadonlyArray<string>): string[] {
  return Array.from(new Set(permissions.filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

export const lazyModuleRouteEntries: LazyModuleRouteEntry[] = [
  {
    moduleCode: "sample",
    path: "/modules/sample",
    permission: "sample.read",
    loadRoutes: async () => (await import("../../../modules/sample-module/routes")).routes as ReadonlyArray<ModuleRoute>,
  },
];

export const frontendModules: FrontendModuleManifest[] = [
  {
    sourceDir: "sample-module",
    packageName: "@unicore/sample-module",
    version: "0.1.0",
    moduleCode: "sample",
    routes: [
      {
        path: "/modules/sample",
        permission: "sample.read",
      },
    ],
    routePaths: collectRoutePaths([
      "/modules/sample",
    ]),
    routePermissions: collectRoutePermissions([
      "sample.read",
    ]),
  },
];