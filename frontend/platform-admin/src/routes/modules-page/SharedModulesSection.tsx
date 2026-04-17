import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { ModuleAlignmentDetail } from "./types";

type SharedModulesSectionProps = {
  alignmentDetails: ModuleAlignmentDetail[];
};

export function SharedModulesSection({ alignmentDetails }: SharedModulesSectionProps) {
  return (
    <PageSection title="前后端共同识别的模块">
      <h4 style={{ marginTop: 0 }}>前后端共同识别的模块</h4>
      <ul>
        {alignmentDetails.length === 0 ? (
          <li>无</li>
        ) : (
          alignmentDetails.map(({ frontendModule, backendModule, missingInBackend, missingInFrontend, menuMismatches }) => (
            <li key={frontendModule.sourceDir}>
              <b>{frontendModule.packageName}</b>{" "}
              （moduleCode={frontendModule.moduleCode}，routes={frontendModule.routes.length}，menus={frontendModule.menus.length}，frontend
              permissions={Object.keys(frontendModule.permissions).length}，backend permissions={backendModule?.permissions.length ?? 0}）
              {missingInBackend.length > 0 ? `；backend 缺少权限: ${missingInBackend.join(", ")}` : ""}
              {missingInFrontend.length > 0 ? `；frontend 缺少权限: ${missingInFrontend.join(", ")}` : ""}
              {menuMismatches.length > 0 ? `；菜单差异: ${menuMismatches.join(" | ")}` : ""}
            </li>
          ))
        )}
      </ul>
    </PageSection>
  );
}
