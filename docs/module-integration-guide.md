# 业务模块接入指南

本文档描述业务模块接入 `UniCore` 平台的最小要求。

## 最小契约

模块需实现 `IBusinessModule`，并至少提供以下内容：

- `Metadata`：模块代码、名称、版本
- `RegisterServices()`：模块依赖注入
- `MapEndpoints()`：模块路由映射
- `GetPermissions()`：权限点声明
- `GetMenus()`：菜单声明
- `GetAuditDeclarations()`：审计事件声明

当前契约还支持可选声明：

- `GetMigrations()`：迁移声明
- `GetEvents()`：领域/集成事件声明

## 权限命名规范

- 统一使用 `module.action` 风格（如 `order.read`、`order.update`）
- 读写权限分离，避免使用泛化权限
- 默认角色建议仅作为初始化参考，最终以运营配置为准

## 审计声明规范

- 关键操作必须声明审计事件（新增、修改、删除、授权、导出）
- `EventCode` 推荐 `module.action` 风格
- `Level` 建议按风险分级：`Info` / `Warn` / `Error`

## 模块模板

可通过模板快速生成模块骨架：

```powershell
dotnet new install ./templates/unicore-business-module-template
dotnet new unicore-module -n OrderModule --ModuleCode order
```

生成后将模块项目加入解决方案，并在 `Platform.WebApi` 引用该项目即可被自动发现。
