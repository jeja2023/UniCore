export type ModulePermissionDto = {
  permissionCode?: string;
  code?: string;
  name?: string;
};

export type ModuleMenuDto = {
  menuCode?: string;
  key?: string;
  routePath?: string;
  path?: string;
  permissionCode?: string;
  permission?: string;
};

export type BackendModuleDto = {
  moduleCode: string;
  moduleName: string;
  moduleVersion: string;
  permissions: ModulePermissionDto[];
  menus?: ModuleMenuDto[];
};

export type ModuleContractsResponse = {
  data: BackendModuleDto[];
};

export type FrontendModuleLike = {
  sourceDir: string;
  packageName: string;
  moduleCode: string;
  routes: ReadonlyArray<{ path: string; permission?: string | null }>;
  routePaths: ReadonlyArray<string>;
  routePermissions: ReadonlyArray<string>;
};

export type ModuleAlignmentDetail = {
  frontendModule: FrontendModuleLike;
  backendModule: BackendModuleDto | undefined;
  missingInBackend: string[];
  backendPermissionsWithoutFrontendRoutes: string[];
  missingFrontendRoutesForMenus: string[];
  routePermissionMismatches: string[];
};
