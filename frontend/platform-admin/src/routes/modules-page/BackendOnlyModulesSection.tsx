import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { BackendModuleDto } from "./types";

type BackendOnlyModulesSectionProps = {
  modules: BackendModuleDto[];
};

export function BackendOnlyModulesSection({ modules }: BackendOnlyModulesSectionProps) {
  return (
    <PageSection title="仅后端模块">
      <h4 style={{ marginTop: 0 }}>后端已注册、前端未挂载的模块</h4>
      <ul>
        {modules.length === 0 ? (
          <li>无</li>
        ) : (
          modules.map((module) => (
            <li key={module.moduleCode}>
              <b>{module.moduleName}</b> {`（${module.moduleCode} / ${module.moduleVersion}）`}
            </li>
          ))
        )}
      </ul>
    </PageSection>
  );
}
