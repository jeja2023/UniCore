namespace Platform.Infrastructure.Persistence.Entities;

public sealed class UserEntity
{
    public Guid UserId { get; set; }
    public string TenantId { get; set; } = "default";
    public string DepartmentCode { get; set; } = "default";
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class RoleEntity
{
    public Guid RoleId { get; set; }
    public string TenantId { get; set; } = "default";
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public sealed class UserRoleEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class RolePermissionEntity
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
}

public sealed class RefreshTokenEntity
{
    public Guid RefreshTokenId { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class AuditEventEntity
{
    public Guid AuditEventId { get; set; }
    public string TenantId { get; set; } = "default";
    public string EventCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string Level { get; set; } = string.Empty;
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }
    public string? TraceId { get; set; }
}

public sealed class AuditExportJobEntity
{
    public Guid JobId { get; set; }
    public string TenantId { get; set; } = "default";
    public string CreatedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CsvContent { get; set; }
    public string? Error { get; set; }
}

public sealed class TenantEntity
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TenantSettingEntity
{
    public Guid TenantSettingId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExternalIdentityLinkEntity
{
    public Guid ExternalIdentityLinkId { get; set; }
    public string TenantId { get; set; } = "default";
    public string Provider { get; set; } = string.Empty;
    public string ExternalUserId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
}

public sealed class RoleDataScopeEntity
{
    public Guid RoleId { get; set; }
    public string Scope { get; set; } = "Tenant";
    public string? CustomExpression { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RoleDataScopeHistoryEntity
{
    public Guid RoleDataScopeHistoryId { get; set; }
    public Guid RoleId { get; set; }
    public string Scope { get; set; } = "Tenant";
    public string? CustomExpression { get; set; }
    public int Version { get; set; }
    public string ChangedBy { get; set; } = "system";
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class NotificationMessageEntity
{
    public Guid NotificationMessageId { get; set; }
    public string TenantId { get; set; } = "default";
    public string Channel { get; set; } = string.Empty;
    public string? Receiver { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? Error { get; set; }
}

public sealed class NotificationTemplateEntity
{
    public Guid NotificationTemplateId { get; set; }
    public string TenantId { get; set; } = "default";
    public string TemplateCode { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public bool Enabled { get; set; } = true;
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NotificationTemplateVersionEntity
{
    public Guid NotificationTemplateVersionId { get; set; }
    public Guid NotificationTemplateId { get; set; }
    public string TenantId { get; set; } = "default";
    public string TemplateCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = "system";
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class ScheduledJobEntity
{
    public Guid ScheduledJobId { get; set; }
    public string TenantId { get; set; } = "default";
    public string JobType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset RunAt { get; set; }
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
}

public sealed class StoredFileObjectEntity
{
    public Guid StoredFileObjectId { get; set; }
    public string TenantId { get; set; } = "default";
    public string FileId { get; set; } = string.Empty;
    public string Provider { get; set; } = "local";
    public string Bucket { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public DateTimeOffset StoredAt { get; set; }
}

public sealed class DictionaryItemEntity
{
    public Guid DictionaryItemId { get; set; }
    public string TenantId { get; set; } = "default";
    public string DictionaryCode { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Sort { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AuditEntityChangeEntity
{
    public Guid AuditEntityChangeId { get; set; }
    public string TenantId { get; set; } = "default";
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Actor { get; set; } = "system";
    public DateTimeOffset OccurredAt { get; set; }
    public string ChangesJson { get; set; } = "{}";
}
