# platform-admin

UniCore 前端管理台基座（React + TypeScript + Vite）。

## 目标

- 登录、路由守卫、动态菜单、按钮级权限控制
- 统一设计系统（tokens/theme/base/patterns）
- 与后端统一协议（权限点、错误码、分页检索参数）

## 目录约定

- `src/design/tokens/` 设计令牌
- `src/design/theme/` 主题逻辑
- `src/components/base/` 基础组件封装
- `src/components/patterns/` 页面模式组件
- `src/docs/design-system/` 规范文档

## 常用命令

在 `frontend/` 目录执行：

```powershell
npm install
npm run lint
npm run build
```

在 `frontend/platform-admin/` 目录执行：

```powershell
npm run check:design-system
```

## 前端模块脚手架

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/new-frontend-module.ps1 -Name order-module -ModuleCode order
```

生成目录：`frontend/modules/order-module/`，包含 `routes/menu/permissions/api` 模板文件。
