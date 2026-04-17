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

生成目录：`frontend/modules/order-module/`，包含 `routes/menu/permissions/api` 模板文件，并自动更新 `src/routes/moduleRegistry.generated.tsx`。

## 模块注册表生成

```powershell
npm run registry:generate
```

用途：

- 扫描 `frontend/modules/*`
- 生成前端路由注册表与模块清单（routes / menus / permissions）
- 对存在 `package.json` 但缺失 `routes/menu/permissions` 导出的模块直接报错，避免模块被静默漏挂

## 模块校验

```powershell
npm run modules:validate
```

用途：

- 检查模块目录中的 `routes/menu/permissions/api/index` 文件是否齐全
- 检查 `package.json exports` 是否完整
- 检查包名是否满足 `@unicore/*`

日常开发时，`dev`、`build`、`lint` 会自动先执行 `modules:sync`。

## 前后端契约对齐检查

```powershell
npm run modules:check-contracts
```

用途：

- 登录后端并读取 `/api/modules/contracts`
- 对比前端模块的 `permissions` 与后端权限声明
- 对比前端模块的 `menu` 与后端菜单声明
- 识别“仅前端存在”或“仅后端存在”的模块
