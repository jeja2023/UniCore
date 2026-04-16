import React from "react";

export function SampleModuleHome() {
  return (
    <div>
      <h3>示例模块页面</h3>
      <div style={{ color: "#667085" }}>
        这是通过“前端业务模块契约”组织的示例页面骨架。
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
] as const;

