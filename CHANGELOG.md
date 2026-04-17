# 变更日志

所有重要变更记录在此文件中。

## [未发布]

### 新增
- 后端接口组织重构：新增 `src/Platform.WebApi/Endpoints/*` 与 `src/Platform.WebApi/Startup/ServiceRegistrationExtensions.cs`，将 `Program` 中的服务注册与路由映射拆分为可维护模块。
- 新增权限注册中心 `src/Platform.WebApi/Auth/PermissionCatalog.cs`，集中维护权限码、策略绑定与平台菜单权限映射。
- 新增当前用户上下文服务 `src/Platform.Identity/Services/CurrentUserContextService.cs`，统一处理用户快照、角色权限解析与权限判定。
- 新增安全/质量 CI：
  - `.github/workflows/backend-quality.yml`（后端构建+测试+覆盖率阈值）
  - `.github/workflows/security-scan.yml`（Gitleaks + 后端漏洞检查 + 前端 audit）
- 新增运维与安全文档：`docs/ops-runbook.md`、`docs/security-baseline.md`。
- 新增前端模块化治理脚本：
  - `frontend/platform-admin/scripts/generate-module-registry.ps1`
  - `frontend/platform-admin/scripts/validate-frontend-modules.ps1`
  - `frontend/platform-admin/scripts/check-module-contract-alignment.ps1`
- 新增前端模块路由/页面拆分与治理基础文件：`frontend/platform-admin/src/routes/appRoutes.tsx`、`routePaths.ts`、`moduleRegistry.tsx`、`moduleRegistry.generated.tsx` 及 `modules-page/*`、`users-page/*`、`data-scope-governance/*`。
- 新增前端基础能力组件：`frontend/platform-admin/src/components/base/StatusText.tsx`、`frontend/platform-admin/src/components/patterns/PageAsyncState.tsx`。
- 新增后端审计索引迁移：`src/Platform.Infrastructure/Persistence/Migrations/20260417042735_AddAuditEventIndexes*.cs`（租户+时间、租户+事件码）。

### 变更
- 审计导出任务从 `Task.Run` 调整为托管后台队列（`Channel + HostedService`），并统一回调发送流程与取消令牌传递：`src/Platform.AuditLog/Services/AuditExportService.cs`。
- 通知与调度链路优化：
  - `NotificationService` 增加外呼重试（指数退避）；
  - `JobSchedulerHostedService` 批量预取消息，减少循环内数据库提交与 N+1 查询。
  - 涉及：`src/Platform.Infrastructure/Services/PlatformExpansionServices.cs`。
- 配置治理增强：多个关键 Options 增加 `ValidateOnStart`/约束校验（Redis、对象存储、通知渠道、OIDC、调度、导出回调/清理）。
- WebApi 鉴权链路解耦：`PermissionAuthorizationHandler` 改为依赖上下文服务，不再直接查询 `AppDbContext`。
- `/api/me/context` 改为通过统一上下文服务构建用户权限与菜单数据，减少 Endpoint 层数据访问职责。
- 审计与调度读路径补充 `AsNoTracking`，降低查询开销：
  - `src/Platform.AuditLog/Services/AuditLogService.cs`
  - `src/Platform.Infrastructure/Services/PlatformExpansionServices.cs`
- 前端管理台路由、页面与安全模块重构，`App.tsx`/`ShellLayout.tsx`/`UsersPage.tsx`/`ModulesPage.tsx`/`LoginPage.tsx` 等改为更细粒度组合与复用。
- 前端 workspace 流程优化：
  - `frontend-quality.yml` 使用 `npm ci`，并增加测试步骤（`--if-present`）；
  - `frontend/platform-admin/package.json` 增补 `modules:sync`、契约校验相关脚本串联。
- `sdk-sync-check.yml` 更新安装方式与检查链路，提升 SDK 同步校验稳定性。
- 项目包管理与工程配置更新：`Directory.Packages.props`、多个 `*.csproj` 与模板工程引用同步调整。

### 修复
- 修复调度器在重试通知任务时的数据库访问低效问题（逐条查库/频繁 `SaveChanges`）。
- 修复审计导出异步任务生命周期不受宿主托管导致的可靠性风险。
- 修复部分只读场景仍启用实体跟踪造成的额外内存与性能损耗。
- 补充日志忽略项 `*.lscache`（`.gitignore`），减少无关文件噪音。

### 安全
- 增加安全扫描工作流（密钥泄漏扫描 + 依赖漏洞检查）。
- 强化配置失败前置（`ValidateOnStart`）与安全基线文档，降低错误配置上线风险。
- 补充租户审计检索关键索引，提升审计数据查询稳定性与可用性。

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

