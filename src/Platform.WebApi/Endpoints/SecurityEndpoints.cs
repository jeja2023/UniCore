using System.Security.Claims;
using Platform.AuditLog.Services;
using Platform.Auth.Services;
using Platform.Core.Abstractions;
using Platform.Core.Common;
using Platform.Identity.Services;
using Platform.Infrastructure.Services;
using Platform.Permission.Services;
using Platform.WebApi.Auth;

namespace Platform.WebApi.Endpoints;

internal static class SecurityEndpoints
{
    internal static void MapSecurityEndpoints(this WebApplication app, JwtOptions jwtOptions)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, AuthService authService, AuditLogService auditLog, HttpContext context) =>
        {
            var result = await authService.LoginAsync(request.Username, request.Password, request.TenantId ?? "default");
            await auditLog.WriteAsync(new AuditEvent("auth.login", "用户登录成功", request.Username, DateTimeOffset.UtcNow, "Info"));
            return Results.Ok(AppResult<LoginResult>.Ok(result, context.TraceIdentifier));
        });

        app.MapPost("/api/auth/refresh", async (RefreshRequest request, AuthService authService, HttpContext context) =>
        {
            var result = await authService.RefreshAsync(request.RefreshToken);
            return Results.Ok(AppResult<LoginResult>.Ok(result, context.TraceIdentifier));
        });

        app.MapPost("/api/auth/logout", async (LogoutRequest request, AuthService authService, IAppCache cache, HttpContext context) =>
        {
            var jti = context.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
            if (!string.IsNullOrWhiteSpace(jti))
            {
                var ttl = TimeSpan.FromMinutes(jwtOptions.AccessTokenExpireMinutes);
                await cache.SetAsync($"auth:blacklist:jti:{jti}", "1", ttl, context.RequestAborted);
            }

            await authService.LogoutAsync(request.RefreshToken ?? string.Empty, context.RequestAborted);
            return Results.Ok(AppResult<string>.Ok("ok", context.TraceIdentifier));
        }).RequireAuthorization();

        app.MapGet("/api/identity/users", async (UserService userService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            return Results.Ok(AppResult<IReadOnlyCollection<UserDto>>.Ok(await userService.GetUsersAsync(requester), context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.UserRead);

        app.MapPost("/api/identity/users", async (CreateUserRequest request, UserService userService, HttpContext context) =>
            Results.Ok(AppResult<UserDto>.Ok(await userService.CreateUserAsync(request.Username, request.DisplayName, request.Password), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.UserCreate);

        app.MapPost("/api/identity/users/{userId}/enabled", async (string userId, SetUserEnabledRequest request, UserService userService, HttpContext context) =>
            Results.Ok(AppResult<UserDto>.Ok(await userService.SetUserEnabledAsync(userId, request.Enabled), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.UserUpdate);

        app.MapPost("/api/identity/users/{userId}/reset-password", async (string userId, ResetPasswordRequest request, UserService userService, HttpContext context) =>
        {
            await userService.ResetPasswordAsync(userId, request.NewPassword);
            return Results.Ok(AppResult<string>.Ok("reset", context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.UserUpdate);

        app.MapGet("/api/identity/users/{userId}/roles", async (string userId, UserService userService, HttpContext context) =>
            Results.Ok(AppResult<IReadOnlyCollection<string>>.Ok(await userService.GetUserRolesAsync(userId), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.UserRead);

        app.MapPost("/api/identity/users/{userId}/roles/assign", async (string userId, AssignRolesRequest request, UserService userService, HttpContext context) =>
            Results.Ok(AppResult<IReadOnlyCollection<string>>.Ok(await userService.AssignRolesAsync(userId, request.Roles), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.UserUpdate);

        app.MapGet("/api/permission/roles/{role}", async (string role, PermissionService permissionService, HttpContext context) =>
            Results.Ok(AppResult<IReadOnlyCollection<string>>.Ok(await permissionService.GetPermissionsByRoleAsync(role), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/permission/roles", async (PermissionService permissionService, HttpContext context) =>
            Results.Ok(AppResult<IReadOnlyCollection<RoleDto>>.Ok(await permissionService.GetRolesAsync(), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/permission/roles", async (CreateRoleRequest request, PermissionService permissionService, HttpContext context) =>
            Results.Ok(AppResult<RoleDto>.Ok(await permissionService.CreateRoleAsync(request.RoleCode, request.RoleName), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/permission/roles/{role}/grant", async (string role, GrantPermissionsRequest request, PermissionService permissionService, HttpContext context) =>
            Results.Ok(AppResult<IReadOnlyCollection<string>>.Ok(await permissionService.GrantPermissionsAsync(role, request.Permissions), context.TraceIdentifier)))
            .RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapPost("/api/permission/roles/{role}/data-scope", async (string role, SetDataScopeRequest request, PermissionService permissionService, AuditLogService auditLogService, HttpContext context) =>
        {
            if (!Enum.TryParse<DataScope>(request.Scope, true, out var scope))
            {
                throw new AppException(ErrorCodes.ValidationError, "scope 仅支持 Self / Department / Tenant / Custom。");
            }

            var requester = EndpointHelpers.GetRequester(context);
            await permissionService.SetRoleDataScopeAsync(role, scope, request.CustomExpression, requester, request.ExpectedRevision);
            await auditLogService.WriteAsync(new AuditEvent(
                "permission.data-scope.updated",
                $"更新角色数据权限: role={role}, scope={scope}",
                requester,
                DateTimeOffset.UtcNow,
                "Info",
                context.Request.Path,
                context.Request.Method,
                StatusCodes.Status200OK,
                context.TraceIdentifier));
            return Results.Ok(AppResult<string>.Ok("ok", context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/permission/roles/{role}/data-scope", async (string role, PermissionService permissionService, HttpContext context) =>
        {
            var detail = await permissionService.GetRoleDataScopeAsync(role);
            if (detail is null)
            {
                throw new AppException(ErrorCodes.NotFound, $"角色不存在: {role}", 404);
            }

            return Results.Ok(AppResult<object>.Ok(new
            {
                detail.RoleCode,
                detail.Scope,
                detail.CustomExpression,
                detail.Revision,
                detail.UpdatedAt
            }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/permission/data-scope/templates", (HttpContext context) =>
        {
            var templates = new[]
            {
                new { Code = "self_only", Name = "仅本人", Scope = "Custom", Expression = "user_id = '{current_user_id}'" },
                new { Code = "same_department", Name = "同部门", Scope = "Custom", Expression = "department_code = '{current_department_code}'" },
                new { Code = "tenant_and_department", Name = "租户内同部门", Scope = "Custom", Expression = "tenant_id = '{current_tenant_id}' AND department_code = '{current_department_code}'" }
            };
            return Results.Ok(AppResult<object>.Ok(templates, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/permission/data-scope/validate", (ValidateDataScopeExpressionRequest request, PermissionService permissionService, HttpContext context) =>
        {
            var result = permissionService.ValidateRoleDataScopeExpression(request.CustomExpression);
            if (!result.IsValid)
            {
                throw new AppException(ErrorCodes.ValidationError, result.ErrorMessage ?? "customExpression 不合法。");
            }

            return Results.Ok(AppResult<object>.Ok(new { isValid = true }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/permission/data-scope/parse", (ValidateDataScopeExpressionRequest request, PermissionService permissionService, HttpContext context) =>
        {
            var result = permissionService.ParseRoleDataScopeExpression(request.CustomExpression);
            if (!result.IsValid)
            {
                throw new AppException(ErrorCodes.ValidationError, result.ErrorMessage ?? "customExpression 不合法。");
            }

            return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/permission/data-scope/compose", (ComposeDataScopeExpressionRequest request, PermissionService permissionService, HttpContext context) =>
        {
            var rules = request.Rules
                .Select(x => new DataScopeExpressionRule(x.Field, x.Operator, x.Value, x.JoinWithPrevious, x.OpenGroupCount, x.CloseGroupCount))
                .ToArray();
            var expression = permissionService.ComposeRoleDataScopeExpression(rules);
            return Results.Ok(AppResult<object>.Ok(new { expression }, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/permission/roles/{role}/data-scope/history", async (string role, int? take, PermissionService permissionService, HttpContext context) =>
        {
            var items = await permissionService.GetRoleDataScopeHistoryAsync(role, take ?? 20);
            return Results.Ok(AppResult<object>.Ok(items, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapPost("/api/permission/roles/{role}/data-scope/rollback", async (string role, RollbackDataScopeRequest request, PermissionService permissionService, AuditLogService auditLogService, HttpContext context) =>
        {
            var requester = EndpointHelpers.GetRequester(context);
            var result = await permissionService.RollbackRoleDataScopeAsync(role, request.TargetVersion, requester);
            await auditLogService.WriteAsync(new AuditEvent(
                "permission.data-scope.rollback",
                $"回滚角色数据权限: role={role}, targetVersion={request.TargetVersion}",
                requester,
                DateTimeOffset.UtcNow,
                "Warn",
                context.Request.Path,
                context.Request.Method,
                StatusCodes.Status200OK,
                context.TraceIdentifier));
            return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionUpdate);

        app.MapGet("/api/permission/roles/{role}/data-scope/diff", async (string role, int fromVersion, int toVersion, PermissionService permissionService, HttpContext context) =>
        {
            var diff = await permissionService.DiffRoleDataScopeVersionsAsync(role, fromVersion, toVersion);
            return Results.Ok(AppResult<object>.Ok(diff, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);

        app.MapGet("/api/permission/data-scope/metadata", (HttpContext context) =>
        {
            var metadata = new
            {
                Fields = new[]
                {
                    new { Code = "tenant_id", Name = "租户 ID", Type = "string" },
                    new { Code = "user_id", Name = "用户 ID", Type = "string" },
                    new { Code = "department_code", Name = "部门编码", Type = "string" }
                },
                Operators = new[] { "=", "!=", ">", ">=", "<", "<=" },
                Joiners = new[] { "AND", "OR" }
            };
            return Results.Ok(AppResult<object>.Ok(metadata, context.TraceIdentifier));
        }).RequireAuthorization(PermissionPolicies.PermissionRead);
    }
}
