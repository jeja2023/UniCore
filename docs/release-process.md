# 版本与发布流程

## 目标

- 保证平台基座版本、NuGet 包版本、前端基座版本可追踪、可回滚。
- 把“开发完成”升级为“可稳定发布”。

## 版本策略

- 基座与公共包遵循 SemVer。
- 破坏性变更：`major`
- 向后兼容新增：`minor`
- 修复与文档：`patch`

## 兼容矩阵

- 发布前必须核对 `docs/compatibility-matrix.md`。
- 发布前必须核对 `docs/lts-support-policy.md` 与 `docs/slo-sli.md`。
- 模块契约校验建议启用 `failOnBreaking=true`，当模块 major 与协议 major 不一致时直接失败。
- 每次发布需要在 `CHANGELOG.md` 记录兼容影响（兼容/需要升级/需要回滚）。

## NuGet 发布流程

1. 更新 `CHANGELOG.md`。
2. 运行打包：

```powershell
pwsh ./packages/build/pack.ps1 -VersionSuffix preview.1
```

3. 校验产物目录：`artifacts/packages/`
4. 在 CI 通过后推送到内部 NuGet feed（或制品仓库）。
5. 记录本次发布版本与回滚目标版本。
6. 生成发布清单（release manifest）并归档：

```powershell
pwsh ./scripts/generate-release-manifest.ps1 -OutputPath artifacts/release/release-manifest.json
```

当前默认打包项目：

- `Platform.Core`
- `Platform.Module.Abstractions`
- `Platform.Auth`
- `Platform.Identity`
- `Platform.Permission`

## 前端发布流程

1. 在 `frontend/` 目录安装依赖并执行：

```powershell
npm run lint
npm run build
```

2. 在 `frontend/platform-admin` 执行设计系统合规检查：

```powershell
pwsh ./scripts/check-design-system-compliance.ps1
```

3. 对应后端接口变更时执行 SDK 校验：

```powershell
npm run -w platform-admin sdk:check
```

4. 执行模块契约对齐检查：

```powershell
npm run -w platform-admin modules:check-contracts
```

5. 可选输出契约报告（阻断 breaking）：

```powershell
curl "http://localhost:5000/api/modules/contracts/report?protocolVersion=1.0.0&failOnBreaking=true"
```

## 发布前 smoke

推荐在发布前执行：

```powershell
pwsh ./scripts/bootstrap-smoke.ps1 -ProjectRoot .
```

企业级发布前置检查（建议默认执行）：

```powershell
pwsh ./scripts/release/preflight-enterprise.ps1
```

## 供应链产物（企业要求）

- 每次发布建议归档以下工件：
  - `artifacts/release/release-manifest.json`
  - `sbom.cyclonedx.json`
- 可通过 CI 工作流 `.github/workflows/supply-chain.yml` 自动生成。

## 回滚策略

- 后端：回退 NuGet 包版本并重新部署。
- 前端：回退 `platform-admin` 发布包或制品版本。
- 若为接口不兼容，优先回滚后端到兼容版本，并补发 SDK。

可执行脚本：

```powershell
pwsh ./scripts/release/deploy.ps1 -Environment staging -BackendVersion 0.1.0 -FrontendVersion 0.1.0 -RunPreflight
pwsh ./scripts/release/deploy.ps1 -Environment staging -BackendVersion 0.1.0 -FrontendVersion 0.1.0 -RunPreflight -InitDatabase -DbHost 127.0.0.1 -DbAdminUser postgres -DbAdminPassword (Read-Host "Postgres admin password" -AsSecureString) -DbAppUser unicore_app -DbAppPassword (Read-Host "UniCore app password" -AsSecureString) -DbName unicore_staging
pwsh ./scripts/release/rollback.ps1 -Environment staging -TargetBackendVersion 0.0.9 -TargetFrontendVersion 0.0.9 -Reason "contract breaking"
```

