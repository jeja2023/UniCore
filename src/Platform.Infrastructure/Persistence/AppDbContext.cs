using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence.Entities;
using System.Collections.Generic;

namespace Platform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<AuditExportJobEntity> AuditExportJobs => Set<AuditExportJobEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<TenantSettingEntity> TenantSettings => Set<TenantSettingEntity>();
    public DbSet<ExternalIdentityLinkEntity> ExternalIdentityLinks => Set<ExternalIdentityLinkEntity>();
    public DbSet<RoleDataScopeEntity> RoleDataScopes => Set<RoleDataScopeEntity>();
    public DbSet<RoleDataScopeHistoryEntity> RoleDataScopeHistories => Set<RoleDataScopeHistoryEntity>();
    public DbSet<NotificationMessageEntity> NotificationMessages => Set<NotificationMessageEntity>();
    public DbSet<NotificationTemplateEntity> NotificationTemplates => Set<NotificationTemplateEntity>();
    public DbSet<NotificationTemplateVersionEntity> NotificationTemplateVersions => Set<NotificationTemplateVersionEntity>();
    public DbSet<ScheduledJobEntity> ScheduledJobs => Set<ScheduledJobEntity>();
    public DbSet<StoredFileObjectEntity> StoredFileObjects => Set<StoredFileObjectEntity>();
    public DbSet<DictionaryItemEntity> DictionaryItems => Set<DictionaryItemEntity>();
    public DbSet<AuditEntityChangeEntity> AuditEntityChanges => Set<AuditEntityChangeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.DepartmentCode).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();
        });

        modelBuilder.Entity<RoleEntity>(e =>
        {
            e.HasKey(x => x.RoleId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.RoleCode).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.RoleCode }).IsUnique();
        });

        modelBuilder.Entity<RefreshTokenEntity>(e =>
        {
            e.HasKey(x => x.RefreshTokenId);
            e.Property(x => x.Token).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.Token).IsUnique();
        });

        modelBuilder.Entity<AuditEventEntity>(e =>
        {
            e.HasKey(x => x.AuditEventId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.EventCode).HasMaxLength(128).IsRequired();
            e.Property(x => x.Level).HasMaxLength(32).IsRequired();
            e.Property(x => x.RequestPath).HasMaxLength(256);
            e.Property(x => x.HttpMethod).HasMaxLength(16);
            e.Property(x => x.TraceId).HasMaxLength(64);
            e.HasIndex(x => new { x.TenantId, x.OccurredAt });
            e.HasIndex(x => new { x.TenantId, x.EventCode });
        });

        modelBuilder.Entity<AuditExportJobEntity>(e =>
        {
            e.HasKey(x => x.JobId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Error).HasMaxLength(1024);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.TenantId, x.CreatedBy });
        });

        modelBuilder.Entity<TenantEntity>(e =>
        {
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.TenantName).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<TenantSettingEntity>(e =>
        {
            e.HasKey(x => x.TenantSettingId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.SettingKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.SettingValue).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.SettingKey }).IsUnique();
        });

        modelBuilder.Entity<ExternalIdentityLinkEntity>(e =>
        {
            e.HasKey(x => x.ExternalIdentityLinkId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExternalUserId).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Provider, x.ExternalUserId }).IsUnique();
        });

        modelBuilder.Entity<RoleDataScopeEntity>(e =>
        {
            e.HasKey(x => x.RoleId);
            e.Property(x => x.Scope).HasMaxLength(32).IsRequired();
            e.Property(x => x.CustomExpression).HasMaxLength(512);
            e.Property(x => x.Revision).HasDefaultValue(0);
        });

        modelBuilder.Entity<RoleDataScopeHistoryEntity>(e =>
        {
            e.HasKey(x => x.RoleDataScopeHistoryId);
            e.Property(x => x.Scope).HasMaxLength(32).IsRequired();
            e.Property(x => x.CustomExpression).HasMaxLength(512);
            e.Property(x => x.ChangedBy).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.RoleId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<NotificationMessageEntity>(e =>
        {
            e.HasKey(x => x.NotificationMessageId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            e.Property(x => x.Receiver).HasMaxLength(256);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Error).HasMaxLength(1024);
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<NotificationTemplateEntity>(e =>
        {
            e.HasKey(x => x.NotificationTemplateId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.TemplateCode).HasMaxLength(128).IsRequired();
            e.Property(x => x.UpdatedBy).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.TemplateCode }).IsUnique();
        });

        modelBuilder.Entity<NotificationTemplateVersionEntity>(e =>
        {
            e.HasKey(x => x.NotificationTemplateVersionId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.TemplateCode).HasMaxLength(128).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ChangedBy).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.NotificationTemplateId, x.Version }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.TemplateCode, x.Version }).IsUnique();
        });

        modelBuilder.Entity<ScheduledJobEntity>(e =>
        {
            e.HasKey(x => x.ScheduledJobId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.JobType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Payload).HasColumnType("text");
            e.Property(x => x.Error).HasMaxLength(1024);
            e.HasIndex(x => new { x.Status, x.RunAt });
        });

        modelBuilder.Entity<StoredFileObjectEntity>(e =>
        {
            e.HasKey(x => x.StoredFileObjectId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.FileId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            e.Property(x => x.Bucket).HasMaxLength(128).IsRequired();
            e.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(256).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.FileId }).IsUnique();
        });

        modelBuilder.Entity<DictionaryItemEntity>(e =>
        {
            e.HasKey(x => x.DictionaryItemId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.DictionaryCode).HasMaxLength(128).IsRequired();
            e.Property(x => x.ItemCode).HasMaxLength(128).IsRequired();
            e.Property(x => x.ItemName).HasMaxLength(256).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.DictionaryCode, x.ItemCode }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.DictionaryCode, x.Sort });
        });

        modelBuilder.Entity<AuditEntityChangeEntity>(e =>
        {
            e.HasKey(x => x.AuditEntityChangeId);
            e.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Actor).HasMaxLength(64).IsRequired();
            e.Property(x => x.ChangesJson).HasColumnType("text");
            e.HasIndex(x => new { x.TenantId, x.EntityName, x.EntityId, x.OccurredAt });
        });

        modelBuilder.Entity<UserRoleEntity>().HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<RolePermissionEntity>().HasKey(x => new { x.RoleId, x.PermissionCode });

        ApplyChineseComments(modelBuilder);
    }

    private static void ApplyChineseComments(ModelBuilder modelBuilder)
    {
        var tableComments = new Dictionary<string, string>
        {
            ["Users"] = "用户表",
            ["Roles"] = "角色表",
            ["UserRoles"] = "用户角色关联表",
            ["RolePermissions"] = "角色权限关联表",
            ["RefreshTokens"] = "刷新令牌表",
            ["AuditEvents"] = "审计事件表",
            ["AuditExportJobs"] = "审计导出任务表",
            ["Tenants"] = "租户表",
            ["TenantSettings"] = "租户配置表",
            ["ExternalIdentityLinks"] = "外部身份绑定表",
            ["RoleDataScopes"] = "角色数据范围表",
            ["RoleDataScopeHistories"] = "角色数据范围历史表",
            ["NotificationMessages"] = "通知消息表",
            ["NotificationTemplates"] = "通知模板表",
            ["NotificationTemplateVersions"] = "通知模板版本表",
            ["ScheduledJobs"] = "调度任务表",
            ["StoredFileObjects"] = "文件存储对象表",
            ["DictionaryItems"] = "字典项表",
            ["AuditEntityChanges"] = "实体变更审计表"
        };

        var propertyComments = new Dictionary<string, string>
        {
            ["UserId"] = "用户标识",
            ["RoleId"] = "角色标识",
            ["TenantId"] = "租户标识",
            ["DepartmentCode"] = "部门编码",
            ["Username"] = "用户名",
            ["DisplayName"] = "显示名称",
            ["PasswordHash"] = "密码哈希",
            ["Enabled"] = "是否启用",
            ["RoleCode"] = "角色编码",
            ["RoleName"] = "角色名称",
            ["PermissionCode"] = "权限编码",
            ["RefreshTokenId"] = "刷新令牌标识",
            ["Token"] = "令牌值",
            ["ExpiresAt"] = "过期时间",
            ["Revoked"] = "是否已撤销",
            ["AuditEventId"] = "审计事件标识",
            ["EventCode"] = "事件编码",
            ["Description"] = "描述",
            ["Actor"] = "操作人",
            ["OccurredAt"] = "发生时间",
            ["Level"] = "级别",
            ["RequestPath"] = "请求路径",
            ["HttpMethod"] = "请求方法",
            ["StatusCode"] = "状态码",
            ["TraceId"] = "链路追踪标识",
            ["JobId"] = "任务标识",
            ["CreatedBy"] = "创建人",
            ["Status"] = "状态",
            ["CreatedAt"] = "创建时间",
            ["Completed"] = "是否完成",
            ["CompletedAt"] = "完成时间",
            ["CsvContent"] = "CSV内容",
            ["Error"] = "错误信息",
            ["TenantName"] = "租户名称",
            ["TenantSettingId"] = "租户配置标识",
            ["SettingKey"] = "配置键",
            ["SettingValue"] = "配置值",
            ["UpdatedAt"] = "更新时间",
            ["ExternalIdentityLinkId"] = "外部身份绑定标识",
            ["Provider"] = "提供方",
            ["ExternalUserId"] = "外部用户标识",
            ["LinkedAt"] = "绑定时间",
            ["Scope"] = "范围",
            ["CustomExpression"] = "自定义表达式",
            ["Revision"] = "修订号",
            ["RoleDataScopeHistoryId"] = "角色数据范围历史标识",
            ["Version"] = "版本号",
            ["ChangedBy"] = "变更人",
            ["ChangedAt"] = "变更时间",
            ["NotificationMessageId"] = "通知消息标识",
            ["Channel"] = "渠道",
            ["Receiver"] = "接收方",
            ["Title"] = "标题",
            ["Content"] = "内容",
            ["RetryCount"] = "重试次数",
            ["SentAt"] = "发送时间",
            ["NotificationTemplateId"] = "通知模板标识",
            ["TemplateCode"] = "模板编码",
            ["CurrentVersion"] = "当前版本",
            ["NotificationTemplateVersionId"] = "通知模板版本标识",
            ["ScheduledJobId"] = "调度任务标识",
            ["JobType"] = "任务类型",
            ["Payload"] = "负载数据",
            ["RunAt"] = "计划执行时间",
            ["MaxRetries"] = "最大重试次数",
            ["FinishedAt"] = "完成时间",
            ["StoredFileObjectId"] = "文件对象标识",
            ["FileId"] = "文件业务标识",
            ["Bucket"] = "存储桶",
            ["ObjectKey"] = "对象键",
            ["FileName"] = "文件名",
            ["ContentType"] = "内容类型",
            ["Length"] = "文件大小",
            ["StoredAt"] = "入库时间",
            ["DictionaryItemId"] = "字典项标识",
            ["DictionaryCode"] = "字典编码",
            ["ItemCode"] = "项编码",
            ["ItemName"] = "项名称",
            ["Sort"] = "排序值",
            ["AuditEntityChangeId"] = "实体变更审计标识",
            ["EntityName"] = "实体名称",
            ["EntityId"] = "实体标识",
            ["ChangesJson"] = "变更内容JSON"
        };

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                if (tableComments.TryGetValue(tableName, out var tableComment))
                {
                    entityType.SetComment(tableComment);
                }
                else
                {
                    entityType.SetComment($"{tableName}表");
                }
            }

            foreach (var property in entityType.GetProperties())
            {
                if (propertyComments.TryGetValue(property.Name, out var propertyComment))
                {
                    property.SetComment(propertyComment);
                }
                else
                {
                    property.SetComment($"{property.Name}字段");
                }
            }
        }
    }
}
