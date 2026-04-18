import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { FrontendModuleLike } from "./types";

type FrontendOnlyModulesSectionProps = {
  modules: FrontendModuleLike[];
};

export function FrontendOnlyModulesSection({ modules }: FrontendOnlyModulesSectionProps) {
  return (
    <PageSection title="Frontend Only Modules">
      <h4 style={{ marginTop: 0 }}>Frontend Only Modules</h4>
      <ul>
        {modules.length === 0 ? (
          <li>None</li>
        ) : (
          modules.map((module) => (
            <li key={module.sourceDir}>
              <b>{module.packageName}</b> {`(sourceDir=${module.sourceDir}, moduleCode=${module.moduleCode})`}
            </li>
          ))
        )}
      </ul>
    </PageSection>
  );
}
