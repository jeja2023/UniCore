# 审计导出异步任务说明

本文档说明审计导出任务的异步处理、完成回调通知，以及后台定时清理机制。

## 接口总览

- 创建导出任务：`POST /api/audit/exports`
- 查询任务列表：`GET /api/audit/exports`
- 查询任务详情：`GET /api/audit/exports/{jobId}`
- 下载导出结果：`GET /api/audit/exports/{jobId}/download`

## 创建任务（支持回调）

`POST /api/audit/exports` 请求体支持可选字段 `callbackUrl`：

- 必须是 `http/https` 绝对地址
- 非法地址会返回 `400`，错误码 `COMMON.VALIDATION_ERROR`

示例：

```json
{
  "filter": {
    "httpMethod": "GET",
    "requestPath": "/api/identity/users",
    "limit": 10
  },
  "fields": ["occurredAt", "eventCode", "actor"],
  "callbackUrl": "https://your-service.example.com/audit-export-callback"
}
```

## 完成即回调通知

任务处理结束后（无论成功或失败），系统会向 `callbackUrl` 发送 `POST` JSON。

注意：

- 回调异常（网络错误、超时、非 2xx）不会影响任务最终状态
- 回调失败不会阻断下载接口，仅记录告警日志

### 回调签名（可选）

可通过配置 `AuditExportCallback:SigningKey` 启用回调签名头：

```json
"AuditExportCallback": {
  "SigningKey": "replace-with-your-secret"
}
```

启用后会附带以下请求头：

- `X-UniCore-Event`：固定值 `audit-export.completed`
- `X-UniCore-Timestamp`：Unix 秒级时间戳
- `X-UniCore-Nonce`：随机 nonce（每次回调不同）
- `X-UniCore-Signature`：十六进制签名串

签名算法：

- 原文：`{timestamp}.{nonce}.{rawBody}`
- 算法：`HMACSHA256(signingKey, originalText)`
- 输出：十六进制大写字符串

### 验签示例（C#）

```csharp
using System.Security.Cryptography;
using System.Text;

static bool VerifySignature(
    string signingKey,
    string timestamp,
    string nonce,
    string rawBody,
    string receivedSignature)
{
    var originalText = $"{timestamp}.{nonce}.{rawBody}";
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(originalText));
    var expected = Convert.ToHexString(hash); // 大写十六进制

    return string.Equals(expected, receivedSignature, StringComparison.OrdinalIgnoreCase);
}
```

### 验签示例（Node.js）

```javascript
import crypto from "node:crypto";

function verifySignature(signingKey, timestamp, nonce, rawBody, receivedSignature) {
  const originalText = `${timestamp}.${nonce}.${rawBody}`;
  const expected = crypto
    .createHmac("sha256", signingKey)
    .update(originalText, "utf8")
    .digest("hex")
    .toUpperCase();

  return expected === String(receivedSignature || "").toUpperCase();
}
```

建议接收端同时做以下校验：

- 校验 `X-UniCore-Event` 必须为 `audit-export.completed`
- 校验 `X-UniCore-Timestamp` 与当前时间差在可接受窗口（如 5 分钟）
- 校验 `X-UniCore-Nonce` 未被使用过（短期缓存防重放）
- 使用请求原始 body（`raw body`）参与验签，避免 JSON 重排导致签名不一致

回调 payload 示例：

```json
{
  "jobId": "2de93cd9-d669-4f4f-b670-9ebf3af690b9",
  "status": "Completed",
  "createdBy": "admin",
  "createdAt": "2026-04-15T10:00:00.0000000+00:00",
  "completedAt": "2026-04-15T10:00:01.0000000+00:00",
  "error": null,
  "downloadUrl": "http://localhost:5000/api/audit/exports/2de93cd9-d669-4f4f-b670-9ebf3af690b9/download"
}
```

字段说明：

- `jobId`：导出任务 ID
- `status`：任务状态（`Completed` 或 `Failed`）
- `createdBy`：任务创建人
- `createdAt`：任务创建时间（UTC）
- `completedAt`：任务完成时间（UTC）
- `error`：失败原因（成功时通常为 `null`）
- `downloadUrl`：下载地址（按当前请求域名拼接）

## 后台定时清理（HostedService）

导出任务清理已服务化为后台定时任务，不再是应用启动时仅执行一次。

配置节：`AuditExportCleanup`

默认配置（`src/Platform.WebApi/appsettings.json`）：

```json
"AuditExportCallback": {
  "SigningKey": ""
},
"AuditExportCleanup": {
  "Enabled": true,
  "Ttl": "24:00:00",
  "RunInterval": "00:30:00"
}
```

配置含义：

- `Enabled`：是否启用后台清理服务
- `Ttl`：任务保留时长，超过即视为过期并删除
- `RunInterval`：后台清理执行周期

## 测试覆盖

当前已覆盖以下集成测试场景：

- 合法 `callbackUrl` 能收到回调
- 非法 `callbackUrl` 返回参数错误
- 回调返回非 2xx 不影响任务完成与下载
- 清理服务按配置周期删除过期任务
