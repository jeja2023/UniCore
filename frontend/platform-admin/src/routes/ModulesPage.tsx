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
      <PageSection title="模块">
        <h3 style={{ margin: 0 }}>模块</h3>
        <div
          style={{
            marginTop: tokens.space.xs,
            color: tokens.colors.textSecondary,
          }}
        >
          展示前端本地模块与后端契约模块，方便检查模块接入对齐情况。
        </div>
      </PageSection>
      <PageAsyncState loading={loading} error={error} loadingText="正在加载模块契约..." />
      <SharedModulesSection alignmentDetails={alignmentDetails} />
      <FrontendOnlyModulesSection modules={frontendOnlyModules} />
      <BackendOnlyModulesSection modules={backendOnlyModules} />
    </div>
  );
}

