# packages

内部 NuGet 包配置与版本策略目录。

当前已落地：

- `Directory.Build.props`：统一包元数据和版本基线
- `build/pack.ps1`：批量打包脚本（当前打包 5 个包：`Platform.Core`、`Platform.Module.Abstractions`、`Platform.Auth`、`Platform.Identity`、`Platform.Permission`）

使用示例：

```powershell
pwsh ./packages/build/pack.ps1 -VersionSuffix preview.1
```

后续建议：

- 建立兼容矩阵（协议版本 / 包版本 / 模块版本）
- 发布前自动化校验 breaking change
- 增加 NuGet feed 发布流水线
