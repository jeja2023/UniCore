# 版本与发布流程

## 目标

- 保证平台基座版本、NuGet 包版本、前端基座版本可追踪、可回滚。
- 把“开发完成”升级为“可稳定发布”。

## 版本策略

- 基座与公共包遵循 SemVer。
- 破坏性变更：`major`
- 向后兼容新增：`minor`
- 修复与文档：`patch`

## NuGet 发布流程

1. 更新 `CHANGELOG.md` 的 Unreleased 内容。
2. 运行打包：

```powershell
pwsh ./packages/build/pack.ps1 -VersionSuffix preview.1
```

3. 校验产物目录：`artifacts/packages/`
4. 在 CI 通过后推送到内部 NuGet feed。
5. 记录本次发布版本与回滚目标版本。

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

## 回滚策略

- 后端：回退 NuGet 包版本并重新部署。
- 前端：回退 `platform-admin` 发布包或制品版本。
- 若为接口不兼容，优先回滚后端到兼容版本，并补发 SDK。

