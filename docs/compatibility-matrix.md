# 兼容矩阵（平台复用）

## 目标

明确平台协议、后端模块、前端模块与 SDK 的兼容关系，避免跨项目升级时出现隐性 breaking change。

## 版本对象

- 协议版本：模块契约协议（示例：`1.0.0`）
- 后端模块版本：`Platform.*` 与业务模块 `moduleVersion`
- 前端模块版本：`frontend/modules/*` 的 `package.json version`
- SDK 版本：`frontend/platform-admin/src/api/sdk/unicore-sdk.ts` 对应生成产物版本

## 兼容规则

- `major` 相同：允许兼容升级（仍需通过契约校验）
- `minor` 升级：允许新增能力，不允许删除既有字段语义
- `patch` 升级：仅修复，不改变接口语义
- `major` 不同：默认视为 breaking，必须显式评审并执行迁移方案

## 校验要求

- 后端契约报告：
  - `GET /api/modules/contracts/report?protocolVersion=<x.y.z>&failOnBreaking=true`
- 前后端契约对齐：
  - `npm run -w platform-admin modules:check-contracts`
- SDK 同步校验：
  - `npm run -w platform-admin sdk:check`

## 发布门禁（建议）

- 必须满足：
  - 契约校验无错误
  - `failOnBreaking=true` 时无 breaking 结果
  - SDK 校验通过且无未提交差异
- 如涉及 breaking：
  - 升级协议 major
  - 在 `CHANGELOG.md` 标注影响范围和迁移步骤
  - 预置回滚目标版本
