import React from "react";
import { LoginPage } from "./LoginPage";
import { ROUTE_PATHS } from "./routePaths";

const HomePage = React.lazy(() => import("./HomePage").then((module) => ({ default: module.HomePage })));
const UsersPage = React.lazy(() => import("./UsersPage").then((module) => ({ default: module.UsersPage })));
const DataScopeGovernancePage = React.lazy(() =>
  import("./DataScopeGovernancePage").then((module) => ({ default: module.DataScopeGovernancePage }))
);
const AuditExportsPage = React.lazy(() =>
  import("./AuditExportsPage").then((module) => ({ default: module.AuditExportsPage }))
);
const ModulesPage = React.lazy(() => import("./ModulesPage").then((module) => ({ default: module.ModulesPage })));

export type AppRouteMeta = {
  key: string;
  path?: string;
  index?: boolean;
  requiresAuth: boolean;
  permission?: string;
  element: React.ReactElement;
};

export const appRoutes: ReadonlyArray<AppRouteMeta> = [
  {
    key: "login",
    path: ROUTE_PATHS.LOGIN,
    requiresAuth: false,
    element: <LoginPage />,
  },
  {
    key: "home",
    index: true,
    requiresAuth: true,
    element: <HomePage />,
  },
  {
    key: "identity-users",
    path: ROUTE_PATHS.IDENTITY_USERS,
    requiresAuth: true,
    element: <UsersPage />,
  },
  {
    key: "permission-data-scope",
    path: ROUTE_PATHS.PERMISSION_DATA_SCOPE,
    requiresAuth: true,
    permission: "permission.read",
    element: <DataScopeGovernancePage />,
  },
  {
    key: "audit-exports",
    path: ROUTE_PATHS.AUDIT_EXPORTS,
    requiresAuth: true,
    permission: "audit.read",
    element: <AuditExportsPage />,
  },
  {
    key: "modules",
    path: ROUTE_PATHS.MODULES,
    requiresAuth: true,
    element: <ModulesPage />,
  },
];

export const publicAppRoutes = appRoutes.filter((route) => !route.requiresAuth);
export const privateAppRoutes = appRoutes.filter((route) => route.requiresAuth);
