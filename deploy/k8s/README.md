# UniCore Kubernetes 基线

此目录提供 `Platform.WebApi` 的生产环境基线清单文件。

## 包含的资源

- Namespace：`unicore`
- ConfigMap：非敏感运行时配置
- Secret：连接字符串、JWT 签名密钥、种子管理员密码
- Deployment：2 个副本，包含健康探针与资源限制
- Service：`ClusterIP`，端口为 `80`

## 应用

```bash
kubectl apply -f deploy/k8s/unicore-webapi.yaml
```

## 必要前置准备

- 集群中必须已存在 PostgreSQL 和 Redis 服务。
- 在启动或扩容应用前执行数据库迁移：

```bash
dotnet ef database update --project src/Platform.Infrastructure/Platform.Infrastructure.csproj --startup-project src/Platform.WebApi/Platform.WebApi.csproj
```

## 加固检查清单

- 替换所有 `change_me_*` 的值。
- 将密钥迁移到企业级密钥管理系统（如 External Secrets、Vault 等）。
- 将镜像标签固定为不可变的发布标签（生产环境避免使用 `latest`）。
- 按照你的平台标准补充 ingress、TLS 与网络策略。
