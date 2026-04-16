## Summary

- 

## Checklist

- [ ] 设计系统合规（未直接引入 `antd` 原始组件，优先使用 `components/base` / `components/patterns`）
- [ ] 权限规范合规（新增路由/按钮均声明权限点，命名遵循 `module.action`）
- [ ] 审计规范合规（关键写操作已定义审计事件或说明不需要审计的原因）
- [ ] 已执行前端质量检查（`npm run lint`、`npm run build`、`npm run check:design-system`）
- [ ] 已执行后端回归检查（`dotnet test`）
- [ ] 更新 `CHANGELOG.md`

## Test Plan

- [ ] 本地手动验证
- [ ] 自动化测试验证

