# 00 Governance

## 章节目标
建立设计系统总则，约束所有模块使用统一组件与交互规范。

## 适用范围
`platform-admin` 基座与全部业务模块前端。

## 强制规则（Must）
- 页面必须优先使用基座组件。
- 权限可见性必须由权限点驱动。
- 禁止硬编码颜色、字号、间距。

## 建议规则（Should）
- 新页面从统一模板（列表/详情/编辑）创建。

## 禁止规则（Must Not）
- 禁止直接覆盖第三方组件默认样式。

## 代码示例（Do/Don't）
- Do：`<UcButton variant="primary" />`
- Don't：`<Button style={{ color: '#1677ff' }} />`

## 验收清单（Checklist）
- [ ] 页面仅使用基座组件/模式组件
- [ ] 样式变量全部来自 tokens
- [ ] PR 含设计系统合规自检
