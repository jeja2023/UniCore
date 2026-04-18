# CI 稳定性清单

本文用于降低 “本地能过、GitHub Actions 失败” 的反复情况，按当前仓库的主要工作流给出快速排查顺序与硬性约束。

## 统一原则

- 所有脚本默认按 `ubuntu-latest` 兼容编写，不依赖仅 Windows 可用行为。
- 临时目录不要只用 `$env:TEMP`，应提供 `RUNNER_TEMP` / `TMPDIR` / `GetTempPath()` 回退。
- 涉及服务启动的检查必须输出可诊断信息（至少包含进程退出码、stdout/stderr 尾日志）。
- 门禁阈值先与当前基线一致，随后按阶段提升，避免“规则正确但永远过不了”。
- 每次改动工作流后，优先重跑对应单条 workflow，避免一次性混合多个变量。

## 工作流逐项检查

### `backend-quality`

- 失败特征
  - `coverage.cobertura.xml` 未找到
  - 覆盖率低于阈值
- 快速检查
  - 测试项目是否包含 `coverlet.collector`
  - `dotnet test --collect:"XPlat Code Coverage"` 是否在正确项目上执行
  - 阈值是否与当前基线一致（`COVERAGE_THRESHOLD`）
- 建议策略
  - 当前先守住可通过阈值，后续每次提升 5% 到目标值

### `sdk-sync-check`

- 失败特征
  - `check-sdk-up-to-date.ps1` 报 `Join-Path ... Path is null`
  - 模块契约对齐步骤直接失败
- 快速检查
  - 临时目录变量是否有跨平台回退
  - `UNICORE_BACKEND_BASE_URL`、账号密码、`tenantId` 是否注入
  - 若仅用于一致性基线校验，不要误开 `-FailOnBreaking`

### `bootstrap-e2e`

- 失败特征
  - `HTTP endpoint not ready ... /swagger/v1/swagger.json`
  - 生成项目后后端进程提前退出
- 快速检查
  - 是否等待足够长（冷启动常 > 80s）
  - 失败时是否输出后端 stdout/stderr 尾日志
  - 临时项目是否在失败时保留（便于下载 artifacts 诊断）

### `codeql`

- 失败特征
  - `JavaScript/TypeScript does not support the autobuild build mode`
- 快速检查
  - `csharp` 使用 `autobuild`
  - `javascript-typescript` 使用 `build-mode: none`
  - `autobuild` 步骤应仅在需要的语言执行

### `container-quality` / 本地 Docker 构建

- 失败特征
  - `dotnet restore UniCore.slnx` 报某个项目文件不存在
- 快速检查
  - `Dockerfile` 是否复制了解决方案引用的全部目录（`src` + `tests`）
  - `.dockerignore` 是否误排除了 restore 所需文件

## 提交前最小自检（建议）

- 后端：`dotnet restore UniCore.slnx && dotnet build UniCore.slnx -c Release`
- 覆盖率：`dotnet test tests/Platform.WebApi.IntegrationTests/Platform.WebApi.IntegrationTests.csproj --collect:"XPlat Code Coverage"`
- 前端：在 `frontend` 下执行 `npm run lint`
- 关键脚本：执行 `scripts/validate-powershell-scripts.ps1`
- Docker：`docker build -t unicore-webapi:local .`

## 出现失败时的处理顺序

1. 先修“环境/路径/启动时序”问题（这些会导致所有逻辑检查失真）。
2. 再修“配置不匹配工具约束”（如 CodeQL build mode）。
3. 最后处理“质量门禁阈值”与“业务契约差异”。

按这个顺序处理，可以显著降低重复返工。
