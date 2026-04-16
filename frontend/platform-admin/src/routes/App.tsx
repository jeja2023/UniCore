import React from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { ShellLayout } from "./ShellLayout";
import { RequireAuth } from "../security/RequireAuth";
import { RequirePermission } from "../security/RequirePermission";
import { HomePage } from "./HomePage";
import { UsersPage } from "./UsersPage";
import { ModulesPage } from "./ModulesPage";
import { DataScopeGovernancePage } from "./DataScopeGovernancePage";
import { SampleModuleHome } from "@unicore/sample-module/routes";

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route
        path="/"
        element={
          <RequireAuth>
            <ShellLayout />
          </RequireAuth>
        }
      >
        <Route index element={<HomePage />} />
        <Route path="identity/users" element={<UsersPage />} />
        <Route
          path="permission/data-scope"
          element={
            <RequirePermission permission="permission.read">
              <DataScopeGovernancePage />
            </RequirePermission>
          }
        />
        <Route path="modules" element={<ModulesPage />} />
        <Route path="modules/sample" element={<SampleModuleHome />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

