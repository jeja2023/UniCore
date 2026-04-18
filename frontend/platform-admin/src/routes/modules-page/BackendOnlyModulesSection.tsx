import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { BackendModuleDto } from "./types";

type BackendOnlyModulesSectionProps = {
  modules: BackendModuleDto[];
};

export function BackendOnlyModulesSection({ modules }: BackendOnlyModulesSectionProps) {
  return (
    <PageSection title="Backend Only Modules">
      <h4 style={{ marginTop: 0 }}>Backend Only Modules</h4>
      <ul>
        {modules.length === 0 ? (
          <li>None</li>
        ) : (
          modules.map((module) => (
            <li key={module.moduleCode}>
              <b>{module.moduleName}</b> {`(${module.moduleCode} / ${module.moduleVersion})`}
            </li>
          ))
        )}
      </ul>
    </PageSection>
  );
}
