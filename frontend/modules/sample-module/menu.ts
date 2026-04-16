import { permissions } from "./permissions";

export const menus = [
  {
    key: "sample.home",
    title: "示例模块",
    path: "/modules/sample",
    permission: permissions.read,
  },
] as const;

