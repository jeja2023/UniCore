import React from "react";

export type ModuleRouteDefinition = {
  path: string;
  element: React.ReactElement;
  permission?: string | null;
};

export const moduleCode = "sample";

export function SampleModuleHome() {
  return (
    <div>
      <h3>Sample Module</h3>
      <div style={{ color: "#667085" }}>
        This page is registered through the frontend module contract.
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
