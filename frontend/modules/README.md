# frontend/modules

该目录用于承载“前端业务模块”。

约定：

- 每个模块一个子目录，如 `frontend/modules/order-module`
- 模块对外导出：
  - `routes`：路由定义
  - `menu`：菜单声明
  - `permissions`：权限点声明（与后端一致）
  - `api`：模块 API 封装（优先复用 `platform-admin/src/api/sdk` 生成产物）

说明：

- `platform-admin` 会通过 `scripts/generate-module-registry.ps1` 自动扫描本目录并生成路由注册表
- 如果模块存在 `package.json` 但缺少 `routes/menu/permissions` 关键导出，生成流程会直接失败，避免模块未挂载却不被发现
- `platform-admin` 还会执行 `validate-frontend-modules.ps1`，校验模块文件与 `package.json exports` 一致性

