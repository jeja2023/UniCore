import React from "react";
import { useTheme } from "../design/theme/ThemeProvider";
import { PageSection } from "../components/patterns/PageSection";
import { PageAsyncState } from "../components/patterns/PageAsyncState";
import { useModulesPage } from "./modules-page/useModulesPage";
import { SharedModulesSection } from "./modules-page/SharedModulesSection";
import { FrontendOnlyModulesSection } from "./modules-page/FrontendOnlyModulesSection";
import { BackendOnlyModulesSection } from "./modules-page/BackendOnlyModulesSection";

export function ModulesPage() {
  const { tokens } = useTheme();
  const { loading, error, alignmentDetails, frontendOnlyModules, backendOnlyModules } = useModulesPage();

  return (
    <div style={{ display: "grid", gap: tokens.space.lg }}>
      <PageSection title="模块契约">
        <h3 style={{ margin: 0 }}>前后端模块对齐</h3>
        <div
          style={{
            marginTop: tokens.space.xs,
            color: tokens.colors.textSecondary,
            lineHeight: 1.65,
          }}
        >
          对照本地前端路由与后端模块契约，检查权限与菜单声明是否一致。
        </div>
      </PageSection>
      <PageAsyncState loading={loading} error={error} loadingText="正在加载模块契约…" />
      <SharedModulesSection alignmentDetails={alignmentDetails} />
      <FrontendOnlyModulesSection modules={frontendOnlyModules} />
      <BackendOnlyModulesSection modules={backendOnlyModules} />
    </div>
  );
}
