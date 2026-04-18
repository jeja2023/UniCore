import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { ModuleAlignmentDetail } from "./types";

type SharedModulesSectionProps = {
  alignmentDetails: ModuleAlignmentDetail[];
};

export function SharedModulesSection({ alignmentDetails }: SharedModulesSectionProps) {
  return (
    <PageSection title="Shared Modules">
      <h4 style={{ marginTop: 0 }}>Shared Modules</h4>
      <ul>
        {alignmentDetails.length === 0 ? (
          <li>None</li>
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
                {`(moduleCode=${frontendModule.moduleCode}, routes=${frontendModule.routes.length}, frontend route permissions=${frontendModule.routePermissions.length}, backend permissions=${backendModule?.permissions.length ?? 0})`}
                {missingInBackend.length > 0 ? ` backend missing permissions: ${missingInBackend.join(", ")}` : ""}
                {backendPermissionsWithoutFrontendRoutes.length > 0
                  ? ` backend-only permissions: ${backendPermissionsWithoutFrontendRoutes.join(", ")}`
                  : ""}
                {missingFrontendRoutesForMenus.length > 0
                  ? ` missing frontend routes: ${missingFrontendRoutesForMenus.join(" | ")}`
                  : ""}
                {routePermissionMismatches.length > 0
                  ? ` route permission mismatches: ${routePermissionMismatches.join(" | ")}`
                  : ""}
              </li>
            )
          )
        )}
      </ul>
    </PageSection>
  );
}
