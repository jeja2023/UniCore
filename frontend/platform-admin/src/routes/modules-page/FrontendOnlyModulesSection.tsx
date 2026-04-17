import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { FrontendModuleLike } from "./types";

type FrontendOnlyModulesSectionProps = {
  modules: FrontendModuleLike[];
};

export function FrontendOnlyModulesSection({ modules }: FrontendOnlyModulesSectionProps) {
  return (
    <PageSection title="仅前端存在的模块">
      <h4 style={{ marginTop: 0 }}>仅前端存在的模块</h4>
      <ul>
        {modules.length === 0 ? (
          <li>无</li>
        ) : (
          modules.map((module) => (
            <li key={module.sourceDir}>
              <b>{module.packageName}</b>（sourceDir={module.sourceDir}，moduleCode={module.moduleCode ?? "未识别"}）
            </li>
          ))
        )}
      </ul>
    </PageSection>
  );
}
