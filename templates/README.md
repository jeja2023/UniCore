# templates

`dotnet new` 模板目录。

当前已落地：

- `unicore-business-module-template`：业务模块模板（含权限、菜单、审计、迁移、事件声明骨架）
- `unicore-platform-template`：平台项目模板（可一键生成最小可运行 WebApi）

本地安装模板示例：

```powershell
dotnet new install ./templates/unicore-business-module-template
dotnet new unicore-module -n OrderModule --ModuleCode order
dotnet new install ./templates/unicore-platform-template
dotnet new unicore-platform -n DemoPlatform
```
