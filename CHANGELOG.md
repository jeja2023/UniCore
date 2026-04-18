# 变更日志

所有重要变更记录在此文件中。

## [0.0.3] - 2026-04-18

### 新增
- 性能与可观测性：`Jwt:UserEnabledCacheSeconds`（默认 15，0 关闭）配合 `IJwtUserEnabledValidationCache` / `JwtUserEnabledValidationCache`，降低 JWT 校验阶段每请求查库；`AuditLogWriteMetricsStore` 在 `GET /metrics` 输出审计异步写入队列相关指标（含队列满丢弃时的 `unicore_audit_write_dropped_total`）。
- Redis 启用时，JWT「用户是否启用」校验缓存改为 `DistributedJwtUserEnabledValidationCache`（基于 `IDistributedCache` 多实例共享）；未启用 Redis 时仍为进程内 `JwtUserEnabledValidationCache`。
- HTTP 请求审计可调：`RequestAudit` 配置节与 `RequestAuditOptions`（`AuditHttpGet` 是否记录 GET、`SamplingPercent` 0~100 采样，0 表示关闭由中间件产生的 HTTP 请求审计写入；启动时校验范围）。
- 指标特性开关辅助：`PlatformFeatureFlags.IsMetricsEnabled` 解析 `FeatureFlags:MetricsEnabled`（兼容 `true/false/0/1`）。
- 集成测试：`FeatureFlags:MetricsEnabled=false` 时校验 `/metrics` 返回 404。
- 运维配置：`Database:ApplyMigrationsOnStartup`（默认 true）可在生产由外部 Job 迁移时关闭应用内 `MigrateAsync`。
- 审计导出可靠性增强：数据库迁移 `src/Platform.Infrastructure/Persistence/Migrations/20260417090000_AddAuditExportRetryAndDlq.cs`，为 `AuditExportJobs` 增加 `RetryCount`、`MaxRetries`、`NextAttemptAt`、`LastAttemptAt`、`DeadLettered` 及调度索引。
- 审计事件子串检索与索引：数据库迁移 `src/Platform.Infrastructure/Persistence/Migrations/20260418120000_EnablePgTrgmAuditEventSubstringIndexes.cs`，启用 `pg_trgm` 扩展，并为 `AuditEvents` 的 `Actor`、`EventCode`、`RequestPath`（部分索引）、`TraceId`（部分索引）建立 GIN（`gin_trgm_ops`）索引。
- 审计列表筛选复用：`src/Platform.AuditLog/Services/AuditEventQueryable.cs` 中 `ApplyAuditListFilters`，在 PostgreSQL 上对路径/操作者/事件码/跟踪号等子串条件使用 `ILIKE`（含 `%`、`_`、`\` 转义）；非 Npgsql 提供程序（如集成测试 InMemory）仍使用 `Contains`。
- 审计导出运维 API：`GET /api/audit/exports`（分页查询）、`GET /api/audit/exports/statuses`、`GET /api/audit/exports/dlq`，以及死信处理 `POST /api/audit/exports/{jobId}/dlq/replay`、`POST /api/audit/exports/{jobId}/dlq/discard`（见 `src/Platform.WebApi/Endpoints/AuditAndModuleEndpoints.cs`）。
- 审计导出可观测性：`src/Platform.AuditLog/Metrics/AuditExportMetricsStore.cs`，在 `GET /metrics` 中输出 `unicore_audit_export_jobs_total` 与 `unicore_audit_export_job_duration_seconds` 等指标（`PlatformFoundationEndpoints`）。
- 应用内事件总线抽象：`src/Platform.Core/Abstractions/AppEvents.cs`（`IAppEventBus` / `AppEvent<TPayload>`）与默认进程内实现 `src/Platform.Infrastructure/Services/InMemoryAppEventBus.cs`。
- 权限版本递增与事件：`src/Platform.Permission/Services/PermissionVersionService.cs`、`PermissionVersionEvents.cs`，在权限变更后发布 `PermissionVersionChanged` 载荷，便于多节点/客户端侧失效策略对齐。
- 管理台「审计导出任务」页面：`frontend/platform-admin/src/routes/AuditExportsPage.tsx`（列表、列配置、状态筛选、死信重放/丢弃等，与后端 DTO 字段对齐）。
- 登录后路由与模块 chunk 预热：`frontend/platform-admin/src/routes/prewarm.ts`，在空闲时段预取高频懒加载资源。
- 前端契约与路由治理测试补充：`ShellLayout.test.tsx`、`moduleRegistry.test.tsx`、`useModulesPage.test.tsx`、`RequirePermission.test.tsx`。
- 示例前端模块改为清单驱动：新增 `frontend/modules/sample-module/manifest.json`，移除独立的 `menu.ts`、`permissions.ts`，由 manifest 描述路由与权限键。
- 项目复用脚手架：新增根目录 `new-project.ps1`，支持通过模板一键创建新平台项目，并自动完成业务模块批量生成、解决方案挂载与 `Platform.WebApi` 项目引用；支持 `-Profile` / `-ListProfiles` / `-ConfigFile` 组合。
- 预置场景配置：新增 `bootstrap-profiles/erp.json`、`bootstrap-profiles/crm.json`、`bootstrap-profiles/ops.json` 与 `bootstrap-profiles/README.md`，支持按业务类型快速起盘。
- 初始化配置样例：新增 `new-project.config.sample.json`，可用配置文件驱动新项目初始化流程。

### 变更
- 平台扩展服务文件拆分：`PlatformExpansionServices.cs` 按域拆为 `ExpansionObjectStorage.cs`、`ExpansionTenantServices.cs`、`ExpansionDataScopeServices.cs`、`ExpansionChannelOptionsAndOidc.cs`、`ExpansionNotificationService.cs`、`ExpansionNotificationTemplateService.cs`、`ExpansionJobScheduling.cs`（命名空间不变）。
- 业务模块发现：磁盘扫描时跳过常见框架与 `Platform.*` 程序集，缩短冷启动；`InMemoryAppEventBus` 补充多实例语义说明。
- `AuditExportService` 扩展重试调度、死信判定与回放/丢弃等业务逻辑；`AuditLogService` 等读路径与导出链路协同调整。
- 审计列表分页读路径：`AuditLogService.QueryPagedAsync` 在 PostgreSQL 上对同一过滤、排序后的查询并行执行总数 `COUNT` 与分页数据查询（降低往返总延迟；EF Core 尚无稳定的单条 `COUNT(*) OVER()` LINQ 映射）；`AuditExportService.QueryAuditRowsAsync` 与列表 API 共用 `ApplyAuditListFilters` 谓词语义。
- 审计异步写入队列：`AuditLogWriteQueue` 将 `BoundedChannelFullMode` 由 `Wait` 改为 `DropWrite`，`IAuditLogWriteQueue.EnqueueAsync` 使用 `TryWrite` 非阻塞入队并返回是否成功；`AuditLogService.WriteAsync` 仅在入队成功时累加 `unicore_audit_write_enqueued_total`，否则累加丢弃计数。
- `AuthService`、`UserService`、`PermissionService`、`PermissionAuthorization` 等与鉴权、租户上下文相关的逻辑补强；`RequestMetricsStore` 与全局指标端点整合审计导出指标；`RequestAuditMiddleware` 接入 `IOptions<RequestAuditOptions>`；`ServiceRegistrationExtensions` 绑定 `RequestAuditOptions` 并按 Redis 启用情况注册 JWT 用户启用缓存实现。
- WebApi 契约 DTO：`ApiEndpointContracts.cs`、`EndpointHelpers.cs` 中审计导出任务 DTO 补充重试/死信相关字段；`Program.cs`、`ServiceRegistrationExtensions.cs`、`appsettings.json` 注册事件总线、导出指标与相关服务；`appsettings.json` 增加 `RequestAudit` 示例配置。
- `GET /metrics`（`PlatformFoundationEndpoints`）与 `RequestMetricsMiddleware` 在指标特性关闭时分别返回 404、跳过请求指标累加；`IJwtUserEnabledValidationCache` 接口注释补充进程内/分布式语义。
- `Directory.Build.props` 工程属性微调。
- 前端：`vite.config.ts` 增加生产构建分包与 `es2022` 目标，并按构建模式仅在非 production 生成 sourcemap；精简部分手工 vendor 分包规则；`AuditExportsPage.tsx` 修正 URL 解析得到的 `tab` 类型以通过 `tsc`；`eslint-plugin-react-hooks` 升级至 `5.2.0`（`package-lock.json` 同步）。
- 前端模块注册与校验脚本增强：`generate-module-registry.ps1`、`validate-frontend-modules.ps1`、`check-module-contract-alignment.ps1`、`new-frontend-module.ps1`、`check-sdk-up-to-date.ps1`。
- `moduleRegistry.tsx` / `moduleRegistry.generated.tsx`、`appRoutes.tsx`、`routePaths.ts`、`ShellLayout.tsx`、`ModulesPage.tsx` 与 `modules-page/*` 适配清单式模块与审计导出导航；`vitest.config.ts` 与 `platform-admin/package.json` 测试配置调整。
- 前端样式治理与全局复用约束：`src/styles/global.css` 增加统一复用类（`u-module-page`、`u-page-title`、`u-text-muted`、`u-display-field`、`u-display-field--compact`、`u-display-field__title`）；浅色主题下增强输入/选择/搜索/只读展示等控件的边框、背景、占位符与聚焦态对比度，深色主题保持兼容。
- 前端模块脚手架与示例模块文案、样式对齐：`scripts/new-frontend-module.ps1` 与 `frontend/modules/sample-module/routes.tsx` 改为默认复用全局样式类并采用中文页面文案。
- 前端模块样式合规校验增强：`scripts/validate-frontend-modules.ps1` 新增规则，禁止模块页面引入私有 `css/scss/sass/less` 与 `style={{...}}` 内联样式，强制复用基座全局样式与组件体系。
- 页面文案中文化与术语统一：`LoginPage.tsx`、`HomePage.tsx`、`AuditExportsPage.tsx`、`modules-page/*`、`users-page/UsersTableSection.tsx` 等页面将非必要英文展示改为中文，并统一为“中文主文案 + 英文缩写括号”风格（如“任务标识（ID）”“全局唯一标识（GUID）”“死信队列（DLQ）”“原始数据（JSON）”）。
- `frontend/package-lock.json` 依赖锁定更新；`frontend/modules/README.md` 说明同步。
- 根目录 `start.ps1` 增加 `-SkipInstall`、`-VerboseCheck` 等参数，并补充启动前路径校验与更清晰的本地启动流程；启动成功后提示可通过 `cd frontend; npm run dev:fast` 跳过模块同步进行前端开发。
- 文档补充：`README.md` 增加“一键创建新项目（推荐）”章节、参数优先级说明与使用提示（含 `ModuleCodes` 两种传参写法）。
- 前端快速开发脚本：`platform-admin` 增加 `dev:fast`（等同 `vite`，不触发 `predev` 的 `modules:sync`）；`frontend/package.json` 增加聚合脚本 `dev:fast`。
- `.github/workflows/sdk-sync-check.yml` 扩展/加固 SDK 同步检查步骤。
- 集成测试 `AuthAndRbacFlowTests*.cs`、`PlatformFoundationAndHealthTests.cs` 覆盖新接口与指标等行为（含指标特性关闭场景）。
- CI：`backend-quality.yml`、`sdk-sync-check.yml`、`security-scan.yml` 为 `actions/setup-dotnet` 启用 NuGet 缓存（`*.csproj` / `global.json`）；`frontend-quality.yml`、`sdk-sync-check.yml`、`security-scan.yml` 为 `actions/setup-node` 启用 npm 缓存（`frontend/package-lock.json`）。

### 影响范围与回归关注点
- 视觉样式影响：`platform-admin` 业务页面中复用 `glass-control` / `u-display-field` 的输入框、下拉框、搜索框、只读显示框在浅色主题下会表现出更高边界对比度；深色主题仅做兼容性验证，无预期视觉回退。
- 工程门禁影响：前端模块开发新增样式约束（禁止模块私有样式文件引入、禁止内联样式）；新建或改造模块若不复用全局样式/基座组件将被 `modules:validate` 阻断。
- 文案展示影响：管理台页面文案进一步中文化，术语展示统一为“中文主文案 + 英文缩写括号”，需重点核验筛选区、列表表头、详情区标签在浅色/深色主题与窄屏下的可读性。
- 重点回归建议：执行 `npm run -w platform-admin modules:validate`、`npm run -w platform-admin build`，并手工回归登录页、审计导出页、模块契约页、用户列表页与数据权限治理页的控件可见性与文案一致性。

### 修复
- 审计导出任务在失败边缘场景下的可恢复性与可运维性（重试、死信、人工重放/丢弃）。
- 用户/权限相关服务中租户隔离与一致性方面的遗留风险点（与本轮 `UserService` 等改动一致）。
- `new-project.ps1` 在 Windows PowerShell 下的编码兼容问题：脚本保存为 UTF-8 BOM，避免中文提示导致解析异常。
- `new-project.ps1` 批量模块创建稳定性问题：修复 `ModuleCodes` 变量名冲突、逗号分隔参数解析不一致，以及模块项目路径假设过于固定导致的挂载失败。
- `unicore-business-module-template` 编译问题：`BusinessModule.cs` 补充 `using Microsoft.AspNetCore.Builder;` 以支持 `MapGet` 扩展方法解析。

## [0.0.2] - 2026-04-17

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
- 新增平台权限单一来源定义：`src/Platform.Core/Security/PlatformPermissionCodes.cs`，统一维护平台内置权限码，避免多处硬编码偏差。
- 新增权限种子定义：`src/Platform.Core/Security/PlatformPermissionSeed.cs`，集中管理管理员/运维角色默认权限集合。
- 新增权限一致性启动校验器：`src/Platform.WebApi/Auth/PermissionStartupValidation.cs`，在应用启动时校验策略、权限码、菜单绑定、种子和业务模块权限声明的一致性。

### 变更
- 审计导出任务从 `Task.Run` 调整为托管后台队列（`Channel + HostedService`），并统一回调发送流程与取消令牌传递：`src/Platform.AuditLog/Services/AuditExportService.cs`。
- 通知与调度链路优化：
  - `NotificationService` 增加外呼重试（指数退避）；
  - `JobSchedulerHostedService` 批量预取消息，减少循环内数据库提交与 N+1 查询。
  - 涉及：`src/Platform.Infrastructure/Services/Expansion*.cs`（原 `PlatformExpansionServices.cs` 已拆分）。
- 配置治理增强：多个关键 Options 增加 `ValidateOnStart`/约束校验（Redis、对象存储、通知渠道、OIDC、调度、导出回调/清理）。
- WebApi 鉴权链路解耦：`PermissionAuthorizationHandler` 改为依赖上下文服务，不再直接查询 `AppDbContext`。
- `/api/me/context` 改为通过统一上下文服务构建用户权限与菜单数据，减少 Endpoint 层数据访问职责。
- 审计与调度读路径补充 `AsNoTracking`，降低查询开销：
  - `src/Platform.AuditLog/Services/AuditLogService.cs`
  - `src/Platform.Infrastructure/Services/Expansion*.cs`
- 前端管理台路由、页面与安全模块重构，`App.tsx`/`ShellLayout.tsx`/`UsersPage.tsx`/`ModulesPage.tsx`/`LoginPage.tsx` 等改为更细粒度组合与复用。
- 前端 workspace 流程优化：
  - `frontend-quality.yml` 使用 `npm ci`，并增加测试步骤（`--if-present`）；
  - `frontend/platform-admin/package.json` 增补 `modules:sync`、契约校验相关脚本串联。
- `sdk-sync-check.yml` 更新安装方式与检查链路，提升 SDK 同步校验稳定性。
- 项目包管理与工程配置更新：`Directory.Packages.props`、多个 `*.csproj` 与模板工程引用同步调整。
- WebApi 权限目录对齐核心权限常量：`src/Platform.WebApi/Auth/PermissionCatalog.cs` 中 `PermissionCodes` 改为引用 `PlatformPermissionCodes`，减少重复定义与维护成本。
- 启动流程增加权限前置校验：`src/Platform.WebApi/Program.cs` 在模块发现后执行 `PermissionStartupValidation.EnsureValidAtStartup`，提前阻断错误权限配置进入运行态。
- 默认数据种子权限改为动态合并：`DbSeeder.SeedAsync` 接收管理员权限集合并在启动时合并平台权限与业务模块权限，避免新增模块权限后管理员角色漏配。
- 示例前端模块权限键名由 `write` 调整为 `update`：`frontend/modules/sample-module/permissions.ts`，与后端权限声明语义保持一致。
- 审计导出链路增加租户隔离：`AuditExportService` 的任务读写、查询与导出明细统一按 `TenantId` 约束。
- 鉴权性能优化：`PermissionAuthorizationHandler` 引入短周期内存缓存，降低高频权限判定对数据库的压力。
- 调度执行链路增加抢占保护：`JobSchedulerHostedService` 通过原子状态更新领取任务，降低多实例重复消费风险。
- 前端认证令牌存储从 `localStorage` 调整为 `sessionStorage`，降低 token 长驻暴露面。
- 前端质量门禁升级：`platform-admin` 引入 `vitest`，并将契约校验纳入 `prebuild`；`frontend-quality.yml` 改为强制执行测试步骤。
- 后端质量门禁升级：`backend-quality.yml` 改为基于 `UniCore.slnx` 进行恢复与构建，扩大改动覆盖面。

### 修复
- 修复调度器在重试通知任务时的数据库访问低效问题（逐条查库/频繁 `SaveChanges`）。
- 修复审计导出异步任务生命周期不受宿主托管导致的可靠性风险。
- 修复部分只读场景仍启用实体跟踪造成的额外内存与性能损耗。
- 补充日志忽略项 `*.lscache`（`.gitignore`），减少无关文件噪音。
- 修复平台权限码在多文件重复维护导致的潜在漂移问题，改为核心常量统一复用。
- 修复管理员初始化权限需要手工同步的问题，改为按平台权限与模块权限自动汇总写入种子。
- 修复示例模块前后端权限命名不一致（`sample.write`/`sample.update`）导致的鉴权歧义。
- 修复审计导出在多租户场景下可能读取跨租户数据的风险。
- 修复用户角色查询未加租户约束导致的潜在越权读取问题。
- 修复审计导出任务在服务重启后长期停留 Processing 的可恢复性问题（启动时自动标记为失败并提示重试）。

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

