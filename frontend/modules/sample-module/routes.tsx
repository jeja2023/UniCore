import React from "react";

export type ModuleRouteDefinition = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export const moduleCode = "sample";

export function SampleModuleHome() {
  return (
    <div className="u-module-page">
      <h3 className="u-page-title">示例模块</h3>
      <div className="u-text-muted">
        此页面通过前端模块契约完成注册。
      </div>
    </div>
  );
}

export const routes = [
  {
    path: "/modules/sample",
    element: <SampleModuleHome />,
    permission: "sample.read",
  },
] as const satisfies readonly ModuleRouteDefinition[];
