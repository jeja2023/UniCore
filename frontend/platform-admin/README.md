# platform-admin

UniCore 前端管理台基座（React + TypeScript + Vite）。

## 目标

- 登录、路由守卫、动态菜单、按钮级权限控制
- 统一设计系统（tokens/theme/base/patterns）
- 与后端统一协议（权限点、错误码、分页检索参数）
- 样式统一复用：业务模块必须复用全局样式与基座组件，不允许模块私有样式体系

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

## 提测回归清单

发布前建议至少完成以下检查：

- 样式可见性：在浅色主题下重点检查输入框、选择框、搜索框、只读显示框边界与聚焦态是否清晰；深色主题确认无视觉回退
- 文案一致性：确认页面文案采用“中文主文案 + 英文缩写括号”格式（如 `任务标识（ID）`、`全局唯一标识（GUID）`、`死信队列（DLQ）`、`原始数据（JSON）`）
- 模块样式约束：确认业务模块未引入私有 `css/scss/sass/less`、未使用内联 `style={{...}}`，优先复用全局样式和基座组件
- 自动化校验：执行 `npm run modules:validate`、`npm run build`，必要时补充 `npm run lint`
- 页面手工回归：至少覆盖登录页、审计导出页、模块契约页、用户列表页、数据权限治理页

## 前端模块脚手架

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/new-frontend-module.ps1 -Name order-module -ModuleCode order
```

生成目录：`frontend/modules/order-module/`，包含 `routes`、`manifest.json`、`api` 模板文件，并自动更新 `src/routes/moduleRegistry.generated.tsx`。

## 模块注册表生成

```powershell
npm run registry:generate
```

用途：

- 扫描 `frontend/modules/*`
- 生成前端路由注册表与模块清单（routes / manifest）
- 对存在 `package.json` 但缺失 `routes` 或 `manifest.json` 的模块直接报错，避免模块被静默漏挂

## 模块校验

```powershell
npm run modules:validate
```

用途：

- 检查模块目录中的 `routes/menu/permissions/api/index` 文件是否齐全
- 检查模块目录中的 `routes` / `manifest.json` / `api/index` 文件是否齐全
- 检查包名是否满足 `@unicore/*`
- 检查模块页面是否违规引入样式文件或使用内联样式（要求复用全局样式/组件）

日常开发时，`dev`、`build`、`lint` 会自动先执行 `modules:sync`。

## 前后端契约对齐检查

```powershell
npm run modules:check-contracts
```

用途：

- 登录后端并读取 `/api/modules/contracts`
- 对比 `manifest.json` 路由权限与后端权限声明
- 对比 `manifest.json` 路由路径与后端菜单声明
- 识别“仅前端存在”或“仅后端存在”的模块
