# 变更日志

所有重要变更记录在此文件中。

## [未发布]

## [0.0.1] - 2026-04-16

### 新增
- 平台后端模块化骨架：`Platform.Core` / `Platform.Auth` / `Platform.Identity` / `Platform.Permission` / `Platform.AuditLog` / `Platform.Module.Abstractions` / `Platform.WebApi`。
- MVP 级接口链路：登录、用户查询/管理、角色权限、审计事件写入与查询。
- 业务模块最小接入契约：模块元信息、服务注册、路由注册、权限声明、菜单声明、审计声明。
- 平台基础能力：健康检查、Prometheus 指标、配置读取、内存缓存、本地文件存储。
- 前端基座 `platform-admin`：React + TypeScript + Vite（登录、路由守卫、动态菜单、权限控制骨架）。
- 前端 workspace（`frontend/package.json`）与示例业务模块 `@unicore/sample-module`。
- 前端模块脚手架脚本：`frontend/platform-admin/scripts/new-frontend-module.ps1`（支持 TS/JS）。
- 设计系统工程合规脚本：`frontend/platform-admin/scripts/check-design-system-compliance.ps1`。
- 前端质量 CI：`frontend-quality.yml`（lint / design-system / build）。
- `dotnet new` 模板与初始化能力：`templates/*/.template.config/template.json`。
- 内部 NuGet 打包与发布能力：`packages/build/pack.ps1`。
- 数据权限治理前端可视化页面（表达式校验/解析/拼装/历史/回滚）。
- 审计导出异步任务：`POST /api/audit/exports`、完成回调 `callbackUrl`（可选）与 HMAC 签名头（`AuditExportCallback:SigningKey`）。
- 审计导出任务后台清理：`HostedService` 周期执行，配置节 `AuditExportCleanup`（`Enabled`/`Ttl`/`RunInterval`）。

### 变更
- 扩展 NuGet 打包脚本：支持一次打包 5 个包（`Platform.Core`、`Platform.Module.Abstractions`、`Platform.Auth`、`Platform.Identity`、`Platform.Permission`）。

