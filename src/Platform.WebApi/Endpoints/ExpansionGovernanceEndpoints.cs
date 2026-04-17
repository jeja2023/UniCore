using Platform.Auth.Services;
using Platform.Core.Common;
using Platform.Identity.Services;
using Platform.Infrastructure.Persistence.Entities;
using Platform.Infrastructure.Services;
using Platform.WebApi.Auth;

namespace Platform.WebApi.Endpoints;

internal static class ExpansionGovernanceEndpoints
{
    internal static void MapExpansionGovernanceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tenants", async (TenantService tenantService, HttpContext context) =>
        {
            var data = await tenantService.GetTenantsAsync();
            var payload = data.Select(x => new { x.TenantId, x.TenantName, x.Enabled, x.CreatedAt }).ToArray();
            return Results.Ok(AppResult<object>.Ok(payload, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/tenants", async (CreateTenantRequest request, TenantService tenantService, HttpContext context) =>
        {
            var tenant = await tenantService.CreateTenantAsync(request.TenantId, request.TenantName);
            return Results.Ok(AppResult<object>.Ok(new { tenant.TenantId, tenant.TenantName, tenant.Enabled, tenant.CreatedAt }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/tenants/{tenantId}/settings", async (string tenantId, TenantService tenantService, HttpContext context) =>
        {
            var settings = await tenantService.GetSettingsAsync(tenantId);
            return Results.Ok(AppResult<IReadOnlyDictionary<string, string>>.Ok(settings, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPut("/api/tenants/{tenantId}/settings/{settingKey}", async (string tenantId, string settingKey, UpdateTenantSettingRequest request, TenantService tenantService, HttpContext context) =>
        {
            var updated = await tenantService.UpsertSettingAsync(tenantId, settingKey, request.SettingValue);
            return Results.Ok(AppResult<object>.Ok(new
            {
                updated.TenantId,
                SettingKey = updated.SettingKey,
                SettingValue = updated.SettingValue,
                updated.UpdatedAt
            }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/auth/sso/login", async (SsoLoginRequest request, ExternalIdentityLinkService linkService, UserService userService, AuthService authService, IConfiguration configuration, HttpContext context) =>
        {
            var enableDirectLogin = configuration.GetValue<bool>("Sso:EnableDirectLogin");
            if (!enableDirectLogin)
            {
                throw new AppException(ErrorCodes.Forbidden, "Direct SSO login is disabled. Use /api/auth/sso/oidc/exchange instead.", 403);
            }
            if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.ExternalUserId))
            {
                throw new AppException(ErrorCodes.ValidationError, "provider 和 externalUserId 不能为空。");
            }

            var tenantId = string.IsNullOrWhiteSpace(request.TenantId) ? "default" : request.TenantId.Trim();
            var userId = await linkService.FindUserIdAsync(tenantId, request.Provider.Trim(), request.ExternalUserId.Trim());
            if (userId is null)
            {
                var created = await userService.CreateUserAsync(
                    username: string.IsNullOrWhiteSpace(request.Username) ? $"sso_{request.Provider}_{request.ExternalUserId}" : request.Username.Trim(),
                    displayName: string.IsNullOrWhiteSpace(request.DisplayName) ? "SSO 用户" : request.DisplayName.Trim(),
                    password: $"sso-{Guid.NewGuid():N}",
                    tenantId: tenantId);
                if (!Guid.TryParse(created.UserId, out var parsedUserId))
                {
                    throw new AppException(ErrorCodes.ValidationError, "SSO 用户创建失败。");
                }

                userId = parsedUserId;
                await linkService.LinkAsync(tenantId, request.Provider.Trim(), request.ExternalUserId.Trim(), parsedUserId);
            }

            var token = await authService.LoginByUserIdAsync(userId.Value);
            return Results.Ok(AppResult<LoginResult>.Ok(token, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/auth/sso/oidc/exchange", async (OidcExchangeRequest request, OidcSsoService oidcSsoService, ExternalIdentityLinkService linkService, UserService userService, AuthService authService, HttpContext context) =>
        {
            var tenantId = string.IsNullOrWhiteSpace(request.TenantId) ? "default" : request.TenantId.Trim();
            var profile = await oidcSsoService.ExchangeCodeAsync(
                provider: request.Provider.Trim(),
                code: request.Code.Trim(),
                redirectUri: request.RedirectUri.Trim(),
                codeVerifier: request.CodeVerifier);

            var userId = await linkService.FindUserIdAsync(tenantId, profile.Provider, profile.Subject);
            if (userId is null)
            {
                var created = await userService.CreateUserAsync(
                    username: !string.IsNullOrWhiteSpace(profile.Username) ? profile.Username! : $"oidc_{profile.Provider}_{profile.Subject}",
                    displayName: !string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.DisplayName! : "OIDC 用户",
                    password: $"oidc-{Guid.NewGuid():N}",
                    tenantId: tenantId);
                if (!Guid.TryParse(created.UserId, out var parsedUserId))
                {
                    throw new AppException(ErrorCodes.ValidationError, "OIDC 用户创建失败。");
                }

                userId = parsedUserId;
                await linkService.LinkAsync(tenantId, profile.Provider, profile.Subject, parsedUserId);
            }

            var token = await authService.LoginByUserIdAsync(userId.Value);
            return Results.Ok(AppResult<LoginResult>.Ok(token, context.TraceIdentifier));
        });

        app.MapPost("/api/notifications/inbox", async (CreateInboxNotificationRequest request, NotificationService notificationService, HttpContext context) =>
        {
            var message = await notificationService.SendInboxAsync(request.Title, request.Content, request.Receiver);
            return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/notifications/webhook", async (CreateWebhookNotificationRequest request, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
        {
            var message = await notificationService.SendWebhookAsync(request.CallbackUrl, request.Title, request.Content);
            if (message.Status == "Failed")
            {
                await jobSchedulingService.EnqueueWebhookRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
            }

            return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel, message.Error }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/notifications/email", async (CreateEmailNotificationRequest request, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
        {
            var message = await notificationService.SendEmailAsync(request.ReceiverEmail, request.Title, request.Content);
            if (message.Status == "Failed")
            {
                await jobSchedulingService.EnqueueEmailRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
            }

            return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel, message.Error }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/notifications/sms", async (CreateSmsNotificationRequest request, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
        {
            var message = await notificationService.SendSmsAsync(request.ReceiverPhone, request.Title, request.Content);
            if (message.Status == "Failed")
            {
                await jobSchedulingService.EnqueueSmsRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
            }

            return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel, message.Error }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/notifications", async (NotificationService notificationService, HttpContext context) =>
        {
            var messages = await notificationService.GetRecentAsync();
            var payload = messages.Select(x => new
            {
                x.NotificationMessageId,
                x.Channel,
                x.Receiver,
                x.Title,
                x.Status,
                x.CreatedAt,
                x.SentAt,
                x.Error
            });
            return Results.Ok(AppResult<object>.Ok(payload, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/notifications/templates/{templateCode}/versions", async (string templateCode, PublishNotificationTemplateVersionRequest request, NotificationTemplateService templateService, HttpContext context) =>
        {
            var changedBy = EndpointHelpers.GetOptionalRequester(context);
            var result = await templateService.PublishVersionAsync(templateCode, request.Title, request.Content, changedBy);
            return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/notifications/templates", async (NotificationTemplateService templateService, HttpContext context) =>
        {
            var templates = await templateService.GetTemplatesAsync();
            return Results.Ok(AppResult<object>.Ok(templates, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/notifications/templates/{templateCode}", async (string templateCode, int? version, NotificationTemplateService templateService, HttpContext context) =>
        {
            var template = await templateService.GetTemplateAsync(templateCode, version);
            if (template is null)
            {
                throw new AppException(ErrorCodes.NotFound, "模板不存在。", 404);
            }

            return Results.Ok(AppResult<object>.Ok(template, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/notifications/templates/{templateCode}/enabled", async (string templateCode, SetNotificationTemplateEnabledRequest request, NotificationTemplateService templateService, HttpContext context) =>
        {
            var changedBy = EndpointHelpers.GetOptionalRequester(context);
            var template = await templateService.SetEnabledAsync(templateCode, request.Enabled, changedBy);
            return Results.Ok(AppResult<object>.Ok(template, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/notifications/templates/{templateCode}/versions", async (string templateCode, int? take, NotificationTemplateService templateService, HttpContext context) =>
        {
            var versions = await templateService.GetVersionsAsync(templateCode, take ?? 20);
            return Results.Ok(AppResult<object>.Ok(versions, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/notifications/templates/{templateCode}/diff", async (string templateCode, int fromVersion, int toVersion, NotificationTemplateService templateService, HttpContext context) =>
        {
            var diff = await templateService.DiffVersionsAsync(templateCode, fromVersion, toVersion);
            return Results.Ok(AppResult<object>.Ok(diff, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/notifications/templates/{templateCode}/variables", async (string templateCode, int? version, NotificationTemplateService templateService, HttpContext context) =>
        {
            var variables = await templateService.GetVariablesAsync(templateCode, version);
            return Results.Ok(AppResult<object>.Ok(new { templateCode, version, variables }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/notifications/templates/{templateCode}/preview", async (string templateCode, PreviewNotificationTemplateRequest request, NotificationTemplateService templateService, HttpContext context) =>
        {
            var rendered = await templateService.RenderAsync(templateCode, request.Version, request.Variables, allowDisabledTemplate: request.AllowDisabledTemplate);
            return Results.Ok(AppResult<object>.Ok(rendered, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/notifications/templates/{templateCode}/rollback", async (string templateCode, RollbackNotificationTemplateRequest request, NotificationTemplateService templateService, HttpContext context) =>
        {
            var changedBy = EndpointHelpers.GetOptionalRequester(context);
            var result = await templateService.RollbackAsync(templateCode, request.TargetVersion, changedBy);
            return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/notifications/template-send", async (SendNotificationByTemplateRequest request, NotificationTemplateService templateService, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
        {
            var rendered = await templateService.RenderAsync(request.TemplateCode, request.Version, request.Variables);
            NotificationMessageEntity message = request.Channel.Trim().ToLowerInvariant() switch
            {
                "inbox" => await notificationService.SendInboxAsync(rendered.Title, rendered.Content, request.Receiver),
                "webhook" => await notificationService.SendWebhookAsync(
                    request.CallbackUrl ?? throw new AppException(ErrorCodes.ValidationError, "webhook channel 必须提供 callbackUrl。"),
                    rendered.Title,
                    rendered.Content),
                "email" => await notificationService.SendEmailAsync(
                    request.ReceiverEmail ?? throw new AppException(ErrorCodes.ValidationError, "email channel 必须提供 receiverEmail。"),
                    rendered.Title,
                    rendered.Content),
                "sms" => await notificationService.SendSmsAsync(
                    request.ReceiverPhone ?? throw new AppException(ErrorCodes.ValidationError, "sms channel 必须提供 receiverPhone。"),
                    rendered.Title,
                    rendered.Content),
                _ => throw new AppException(ErrorCodes.ValidationError, "channel 仅支持 inbox/webhook/email/sms。")
            };

            if (message.Status == "Failed")
            {
                if (string.Equals(message.Channel, "Webhook", StringComparison.OrdinalIgnoreCase))
                {
                    await jobSchedulingService.EnqueueWebhookRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
                }
                else if (string.Equals(message.Channel, "Email", StringComparison.OrdinalIgnoreCase))
                {
                    await jobSchedulingService.EnqueueEmailRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
                }
                else if (string.Equals(message.Channel, "Sms", StringComparison.OrdinalIgnoreCase))
                {
                    await jobSchedulingService.EnqueueSmsRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
                }
            }

            return Results.Ok(AppResult<object>.Ok(new
            {
                message.NotificationMessageId,
                message.Channel,
                message.Status,
                message.Error,
                RenderedTitle = rendered.Title,
                RenderedContent = rendered.Content,
                rendered.Version
            }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/scheduling/webhook-retry", async (CreateWebhookRetryJobRequest request, JobSchedulingService jobSchedulingService, HttpContext context) =>
        {
            var job = await jobSchedulingService.EnqueueWebhookRetryAsync(request.NotificationMessageId, request.RunAt ?? DateTimeOffset.UtcNow.AddMinutes(1));
            return Results.Ok(AppResult<object>.Ok(new { job.ScheduledJobId, job.JobType, job.Status, job.RunAt }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/scheduling/jobs", async (JobSchedulingService jobSchedulingService, HttpContext context) =>
        {
            var jobs = await jobSchedulingService.GetJobsAsync();
            var payload = jobs.Select(x => new
            {
                x.ScheduledJobId,
                x.JobType,
                x.Status,
                x.RetryCount,
                x.MaxRetries,
                x.RunAt,
                x.CreatedAt,
                x.FinishedAt,
                x.Error
            });
            return Results.Ok(AppResult<object>.Ok(payload, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/dictionaries", async (DictionaryService dictionaryService, HttpContext context) =>
        {
            var items = await dictionaryService.GetDictionariesAsync(context.RequestAborted);
            return Results.Ok(AppResult<object>.Ok(items, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/dictionaries/{dictionaryCode}/items", async (string dictionaryCode, DictionaryService dictionaryService, HttpContext context) =>
        {
            var items = await dictionaryService.GetItemsAsync(dictionaryCode, context.RequestAborted);
            return Results.Ok(AppResult<object>.Ok(items, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPut("/api/dictionaries/{dictionaryCode}/items/{itemCode}", async (
            string dictionaryCode,
            string itemCode,
            UpsertDictionaryItemRequest request,
            DictionaryService dictionaryService,
            HttpContext context) =>
        {
            var result = await dictionaryService.UpsertItemAsync(dictionaryCode, itemCode, request.ItemName, request.Sort, request.Enabled, context.RequestAborted);
            return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapDelete("/api/dictionaries/{dictionaryCode}/items/{itemCode}", async (
            string dictionaryCode,
            string itemCode,
            DictionaryService dictionaryService,
            HttpContext context) =>
        {
            await dictionaryService.DeleteItemAsync(dictionaryCode, itemCode, context.RequestAborted);
            return Results.Ok(AppResult<string>.Ok("deleted", context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/audit/entity-changes", async (
            string entityName,
            string entityId,
            int? take,
            EntityChangeAuditService auditService,
            HttpContext context) =>
        {
            var items = await auditService.QueryAsync(entityName.Trim(), entityId.Trim(), take ?? 50, context.RequestAborted);
            return Results.Ok(AppResult<object>.Ok(items, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.AuditRead);
    }
}
