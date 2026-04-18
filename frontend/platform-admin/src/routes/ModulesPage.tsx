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
      <PageSection title="Modules">
        <h3 style={{ margin: 0 }}>Modules</h3>
        <div
          style={{
            marginTop: tokens.space.xs,
            color: tokens.colors.textSecondary,
          }}
        >
          Review how local frontend routes align with backend module contracts.
        </div>
      </PageSection>
      <PageAsyncState loading={loading} error={error} loadingText="Loading module contracts..." />
      <SharedModulesSection alignmentDetails={alignmentDetails} />
      <FrontendOnlyModulesSection modules={frontendOnlyModules} />
      <BackendOnlyModulesSection modules={backendOnlyModules} />
    </div>
  );
}
