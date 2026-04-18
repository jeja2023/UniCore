import React from "react";
import { PageSection } from "../../components/patterns/PageSection";
import type { FrontendModuleLike } from "./types";

type FrontendOnlyModulesSectionProps = {
  modules: FrontendModuleLike[];
};

export function FrontendOnlyModulesSection({ modules }: FrontendOnlyModulesSectionProps) {
  return (
    <PageSection title="仅前端模块">
      <h4 style={{ marginTop: 0 }}>本地存在、后端未匹配的模块</h4>
      <ul>
        {modules.length === 0 ? (
          <li>无</li>
        ) : (
          modules.map((module) => (
            <li key={module.sourceDir}>
              <b>{module.packageName}</b> {`（来源目录=${module.sourceDir}，模块编码=${module.moduleCode}）`}
            </li>
          ))
        )}
      </ul>
    </PageSection>
  );
}
