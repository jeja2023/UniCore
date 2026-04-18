# 预置场景配置说明

`new-project.ps1` 支持从当前目录读取预置场景配置（Profile）。

每个场景文件为 `<profile>.json`，可包含以下字段：

- `DestinationRoot`
- `ModuleName`
- `ModuleCode`
- `SecondModule`
- `AdditionalModules`
- `ModuleCodes`
- `SkipTemplateInstall`

查看可用场景：

```powershell
.\new-project.ps1 -ListProfiles
```

使用场景创建项目：

```powershell
.\new-project.ps1 -ProjectName AcmeOpsPlatform -Profile erp -DestinationRoot e:\Projects
```

当前内置场景：

- `erp`：`order`、`inventory`、`procurement`、`finance`
- `crm`：`customer`、`lead`、`opportunity`、`contract`
- `ops`：`ticket`、`monitoring`、`alert`、`change`
