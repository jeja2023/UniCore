using Platform.AuditLog.Services;

namespace Platform.WebApi.Endpoints;

internal sealed record LoginRequest(string Username, string Password, string? TenantId);
internal sealed record RefreshRequest(string RefreshToken);
internal sealed record LogoutRequest(string? RefreshToken);
internal sealed record SsoLoginRequest(string Provider, string ExternalUserId, string? Username, string? DisplayName, string? TenantId);
internal sealed record OidcExchangeRequest(string Provider, string Code, string RedirectUri, string? CodeVerifier, string? TenantId);
internal sealed record WriteAuditRequest(
    string EventCode,
    string Description,
    string Actor,
    string Level,
    string? RequestPath,
    string? HttpMethod,
    int? StatusCode,
    string? TraceId);
internal sealed record CreateRoleRequest(string RoleCode, string RoleName);
internal sealed record GrantPermissionsRequest(IReadOnlyCollection<string> Permissions);
internal sealed record SetDataScopeRequest(string Scope, string? CustomExpression, int? ExpectedRevision);
internal sealed record ValidateDataScopeExpressionRequest(string? CustomExpression);
internal sealed record ComposeDataScopeExpressionRequest(IReadOnlyCollection<ComposeDataScopeExpressionRuleRequest> Rules);
internal sealed record ComposeDataScopeExpressionRuleRequest(
    string Field,
    string Operator,
    string Value,
    string? JoinWithPrevious,
    int OpenGroupCount,
    int CloseGroupCount);
internal sealed record RollbackDataScopeRequest(int TargetVersion);
internal sealed record AssignRolesRequest(IReadOnlyCollection<string> Roles);
internal sealed record CreateUserRequest(string Username, string DisplayName, string Password);
internal sealed record CreateTenantRequest(string TenantId, string TenantName);
internal sealed record UpdateTenantSettingRequest(string SettingValue);
internal sealed record SetUserEnabledRequest(bool Enabled);
internal sealed record ResetPasswordRequest(string NewPassword);
internal sealed record CreateInboxNotificationRequest(string Title, string Content, string? Receiver);
internal sealed record CreateWebhookNotificationRequest(string CallbackUrl, string Title, string Content);
internal sealed record CreateEmailNotificationRequest(string ReceiverEmail, string Title, string Content);
internal sealed record CreateSmsNotificationRequest(string ReceiverPhone, string Title, string Content);
internal sealed record PublishNotificationTemplateVersionRequest(string Title, string Content);
internal sealed record SetNotificationTemplateEnabledRequest(bool Enabled);
internal sealed record PreviewNotificationTemplateRequest(int? Version, IReadOnlyDictionary<string, string>? Variables, bool AllowDisabledTemplate = false);
internal sealed record RollbackNotificationTemplateRequest(int TargetVersion);
internal sealed record SendNotificationByTemplateRequest(
    string TemplateCode,
    string Channel,
    int? Version,
    IReadOnlyDictionary<string, string>? Variables,
    string? Receiver,
    string? CallbackUrl,
    string? ReceiverEmail,
    string? ReceiverPhone);
internal sealed record CreateWebhookRetryJobRequest(Guid NotificationMessageId, DateTimeOffset? RunAt);
internal sealed record UpsertDictionaryItemRequest(string ItemName, int Sort, bool Enabled);
internal sealed record ValidateModuleContractsRequest(string? ProtocolVersion);
internal sealed record CreateAuditExportRequest(AuditQueryFilter? Filter, IReadOnlyCollection<string>? Fields, string? CallbackUrl);
internal sealed record SetCacheRequest(string Value, int? TtlSeconds);
internal sealed record AuditExportJobDto(
    Guid JobId,
    string CreatedBy,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    int RetryCount,
    int MaxRetries,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    bool DeadLettered);
internal sealed record AuditExportJobPageDto(IReadOnlyCollection<AuditExportJobDto> Items, int Total, int Page, int PageSize);
internal sealed record MenuItemDto(string Key, string Title, string Path, string? Permission);
internal sealed record CurrentUserContextDto(
    string UserId,
    string Username,
    string DisplayName,
    string TenantId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<MenuItemDto> Menus);
