import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { ModuleAlignmentDetail } from "./types";

type SharedModulesSectionProps = {
  alignmentDetails: ModuleAlignmentDetail[];
};

export function SharedModulesSection({ alignmentDetails }: SharedModulesSectionProps) {
  return (
    <PageSection title="前后端共有模块">
      <h4 style={{ marginTop: 0 }}>对齐明细</h4>
      <ul>
        {alignmentDetails.length === 0 ? (
          <li>无</li>
        ) : (
          alignmentDetails.map(
            ({
              frontendModule,
              backendModule,
              missingInBackend,
              backendPermissionsWithoutFrontendRoutes,
              missingFrontendRoutesForMenus,
              routePermissionMismatches,
            }) => (
              <li key={frontendModule.sourceDir}>
                <b>{frontendModule.packageName}</b>{" "}
                {`（模块编码=${frontendModule.moduleCode}，路由数=${frontendModule.routes.length}，前端路由权限数=${frontendModule.routePermissions.length}，后端权限数=${backendModule?.permissions.length ?? 0}）`}
                {missingInBackend.length > 0 ? `；后端缺少权限：${missingInBackend.join("，")}` : ""}
                {backendPermissionsWithoutFrontendRoutes.length > 0
                  ? `；仅后端存在权限：${backendPermissionsWithoutFrontendRoutes.join("，")}`
                  : ""}
                {missingFrontendRoutesForMenus.length > 0
                  ? `；菜单缺少前端路由：${missingFrontendRoutesForMenus.join(" | ")}`
                  : ""}
                {routePermissionMismatches.length > 0
                  ? `；路由权限不一致：${routePermissionMismatches.join(" | ")}`
                  : ""}
              </li>
            ),
          )
        )}
      </ul>
    </PageSection>
  );
}
