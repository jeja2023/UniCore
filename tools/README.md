# tools 目录说明

该目录用于统一放置仓库常用工具脚本入口，便于团队成员快速发现与使用。

当前提供：

- `new-project.ps1`：代理到仓库根目录 `new-project.ps1`
- `start.ps1`：代理到仓库根目录 `start.ps1`

示例：

```powershell
cd tools
.\new-project.ps1 -ProjectName DemoPlatform -DestinationRoot e:\Projects -Profile erp
.\start.ps1 -SkipInstall
```

说明：

- 现阶段为兼容旧命令，根目录脚本仍保留可直接运行。
- 后续若新增脚本，建议统一在 `tools` 下增加入口并补充本说明。
