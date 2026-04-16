# frontend/modules

该目录用于承载“前端业务模块”。

约定：

- 每个模块一个子目录，如 `frontend/modules/order-module`
- 模块对外导出：
  - `routes`：路由定义
  - `menu`：菜单声明
  - `permissions`：权限点声明（与后端一致）
  - `api`：模块 API 封装（优先复用 `platform-admin/src/api/sdk` 生成产物）

