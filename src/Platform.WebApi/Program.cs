using Platform.AuditLog.Services;
using Platform.Auth.Services;
using Platform.Core.Abstractions;
using Platform.Core.Common;
using Platform.Identity.Services;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Services;
using Platform.Infrastructure.Persistence.Entities;
using Platform.Module.Abstractions.Contracts;
using Platform.Permission.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using Platform.WebApi.Middleware;
using Platform.WebApi.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.WebApi.OpenApi;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Platform.WebApi.Health;
using Platform.WebApi.Metrics;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<RequestMetricsStore>();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    options.OperationFilter<StandardErrorResponsesOperationFilter>();
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<LoginSecurityOptions>(builder.Configuration.GetSection(LoginSecurityOptions.Section));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
    if (useInMemory)
    {
        options.UseInMemoryDatabase("unicore-test-db");
        return;
    }

    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        connectionString = "Host=localhost;Port=5432;Database=unicore;Username=postgres;Password=postgres";
    }
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    options.UseNpgsql(connectionString);
    options.ReplaceService<IHistoryRepository, CommentedNpgsqlHistoryRepository>();
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (string.IsNullOrWhiteSpace(jti))
                {
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IAppCache>();
                var revoked = await cache.GetAsync<string>($"auth:blacklist:jti:{jti}", context.HttpContext.RequestAborted);
                if (!string.IsNullOrEmpty(revoked))
                {
                    context.Fail("token revoked");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PermissionPolicies.UserRead, p => p.Requirements.Add(new PermissionRequirement("user.read")));
    options.AddPolicy(PermissionPolicies.UserCreate, p => p.Requirements.Add(new PermissionRequirement("user.create")));
    options.AddPolicy(PermissionPolicies.UserUpdate, p => p.Requirements.Add(new PermissionRequirement("user.update")));
    options.AddPolicy(PermissionPolicies.PermissionRead, p => p.Requirements.Add(new PermissionRequirement("permission.read")));
    options.AddPolicy(PermissionPolicies.PermissionUpdate, p => p.Requirements.Add(new PermissionRequirement("permission.update")));
    options.AddPolicy(PermissionPolicies.AuditRead, p => p.Requirements.Add(new PermissionRequirement("audit.read")));
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<AuditExportService>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<ExternalIdentityLinkService>();
builder.Services.AddScoped<DataScopeService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NotificationTemplateService>();
builder.Services.AddScoped<JobSchedulingService>();
builder.Services.AddScoped<OidcSsoService>();
builder.Services.AddScoped<DictionaryService>();
builder.Services.AddScoped<EntityChangeAuditService>();
builder.Services.Configure<RedisCacheOptions>(builder.Configuration.GetSection(RedisCacheOptions.Section));
var redisOptions = builder.Configuration.GetSection(RedisCacheOptions.Section).Get<RedisCacheOptions>() ?? new RedisCacheOptions();
if (redisOptions.Enabled && !string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisOptions.ConnectionString; });
    builder.Services.AddSingleton<IAppCache, DistributedAppCache>();
}
else
{
    builder.Services.AddSingleton<IAppCache, InMemoryAppCache>();
}
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();
builder.Services.Configure<ObjectStorageOptions>(builder.Configuration.GetSection(ObjectStorageOptions.Section));
var objectStorageOptions = builder.Configuration.GetSection(ObjectStorageOptions.Section).Get<ObjectStorageOptions>() ?? new ObjectStorageOptions();
builder.Services.Configure<LocalFileStorageOptions>(builder.Configuration.GetSection(LocalFileStorageOptions.Section));
builder.Services.AddScoped<IFileStorage>(sp =>
{
    var tenantAccessor = sp.GetRequiredService<ITenantContextAccessor>();
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    if (objectStorageOptions.Enabled)
    {
        var s3 = new S3FileStorage(objectStorageOptions);
        return new TrackingFileStorage(s3, scopeFactory, tenantAccessor, "s3", objectStorageOptions.BucketName);
    }

    var local = new LocalFileStorage(sp.GetRequiredService<IOptions<LocalFileStorageOptions>>().Value);
    return new TrackingFileStorage(local, scopeFactory, tenantAccessor, "local", "local");
});
builder.Services.AddHttpClient();
builder.Services
    .AddHealthChecks()
    .AddCheck<AppDbContextHealthCheck>("database", failureStatus: HealthStatus.Unhealthy);
builder.Services.Configure<AuditExportCallbackOptions>(
    builder.Configuration.GetSection(AuditExportCallbackOptions.Section));
builder.Services.Configure<AuditExportCleanupOptions>(
    builder.Configuration.GetSection(AuditExportCleanupOptions.Section));
builder.Services.Configure<JobSchedulingOptions>(builder.Configuration.GetSection(JobSchedulingOptions.Section));
builder.Services.Configure<EmailChannelOptions>(builder.Configuration.GetSection(EmailChannelOptions.Section));
builder.Services.Configure<SmsChannelOptions>(builder.Configuration.GetSection(SmsChannelOptions.Section));
builder.Services.Configure<OidcSsoOptions>(builder.Configuration.GetSection(OidcSsoOptions.Section));
builder.Services.AddHostedService<AuditExportCleanupHostedService>();
builder.Services.AddHostedService<JobSchedulerHostedService>();

var discoveredModules = Assembly.GetExecutingAssembly()
    .GetReferencedAssemblies()
    .Select(Assembly.Load)
    .SelectMany(x => x.GetTypes())
    .Where(t => typeof(IBusinessModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .Select(t => (IBusinessModule)Activator.CreateInstance(t)!)
    .ToArray();

foreach (var module in discoveredModules)
{
    module.RegisterServices(builder.Services);
}

builder.Services.AddSingleton<IReadOnlyCollection<IBusinessModule>>(discoveredModules);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var recreateOnStartup = string.Equals(
        Environment.GetEnvironmentVariable("UNICORE_RECREATE_ON_STARTUP"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    await DbSeeder.SeedAsync(dbContext, recreateOnStartup);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<RequestMetricsMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestAuditMiddleware>();

app.Use(async (context, next) =>
{
    context.TraceIdentifier = context.TraceIdentifier.Length == 0
        ? Guid.NewGuid().ToString("N")
        : context.TraceIdentifier;
    await next();
});

app.MapGet("/api/health", (HttpContext context) =>
    Results.Ok(AppResult<string>.Ok("ok", context.TraceIdentifier)));
app.MapGet("/metrics", (RequestMetricsStore metricsStore) =>
    Results.Text(metricsStore.ToPrometheusText(), "text/plain; version=0.0.4"));
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthResponseAsync
});
app.MapGet("/api/platform/config/features", (IConfiguration configuration, HttpContext context) =>
{
    var flags = configuration.GetSection("FeatureFlags").GetChildren()
        .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    return Results.Ok(AppResult<IReadOnlyDictionary<string, string>>.Ok(flags, context.TraceIdentifier));
});
app.MapPost("/api/platform/cache/{key}", async (string key, SetCacheRequest request, IAppCache cache, HttpContext context) =>
{
    await cache.SetAsync(key, request.Value, request.TtlSeconds.HasValue ? TimeSpan.FromSeconds(request.TtlSeconds.Value) : null);
    return Results.Ok(AppResult<string>.Ok("cached", context.TraceIdentifier));
});
app.MapGet("/api/platform/cache/{key}", async (string key, IAppCache cache, HttpContext context) =>
{
    var value = await cache.GetAsync<string>(key);
    return Results.Ok(AppResult<string?>.Ok(value, context.TraceIdentifier));
});
app.MapDelete("/api/platform/cache/{key}", async (string key, IAppCache cache, HttpContext context) =>
{
    await cache.RemoveAsync(key);
    return Results.Ok(AppResult<string>.Ok("removed", context.TraceIdentifier));
});
app.MapPost("/api/platform/files/upload", async (HttpRequest request, IFileStorage fileStorage, HttpContext context) =>
{
    if (!request.HasFormContentType)
    {
        throw new AppException(ErrorCodes.ValidationError, "请使用 multipart/form-data 上传文件。");
    }

    var form = await request.ReadFormAsync();
    var file = form.Files["file"] ?? form.Files.FirstOrDefault();
    if (file is null || file.Length <= 0)
    {
        throw new AppException(ErrorCodes.ValidationError, "未检测到有效文件。");
    }

    await using var stream = file.OpenReadStream();
    var saved = await fileStorage.SaveAsync(stream, file.FileName, file.ContentType);
    return Results.Ok(AppResult<StoredFileInfo>.Ok(saved, context.TraceIdentifier));
});
app.MapGet("/api/platform/files/{fileId}", async (string fileId, IFileStorage fileStorage) =>
{
    var file = await fileStorage.ReadAsync(fileId);
    if (file is null)
    {
        throw new AppException(ErrorCodes.NotFound, "文件不存在。");
    }

    return Results.File(file.Content, file.ContentType, file.FileName);
});

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
    var jti = context.User.FindFirstValue(JwtRegisteredClaimNames.Jti);
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
    var requester = GetRequester(context);
    return Results.Ok(AppResult<IReadOnlyCollection<UserDto>>.Ok(await userService.GetUsersAsync(requester), context.TraceIdentifier));
})
    .RequireAuthorization(PermissionPolicies.UserRead);

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

    var requester = GetRequester(context);
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
    var requester = GetRequester(context);
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

app.MapPost("/api/audit/events", async (WriteAuditRequest request, AuditLogService auditLogService, HttpContext context) =>
{
    await auditLogService.WriteAsync(new AuditEvent(
        request.EventCode,
        request.Description,
        request.Actor,
        DateTimeOffset.UtcNow,
        request.Level,
        request.RequestPath,
        request.HttpMethod,
        request.StatusCode,
        request.TraceId));
    return Results.Ok(AppResult<string>.Ok("created", context.TraceIdentifier));
}).RequireAuthorization(PermissionPolicies.AuditRead);

app.MapGet("/api/audit/events", async (AuditLogService auditLogService, HttpContext context) =>
{
    var filter = BuildAuditFilter(context.Request.Query);
    var data = await auditLogService.QueryPagedAsync(filter);
    return Results.Ok(AppResult<AuditQueryResult>.Ok(data, context.TraceIdentifier));
})
    .RequireAuthorization(PermissionPolicies.AuditRead);

app.MapGet("/api/audit/events/export", async (AuditLogService auditLogService, HttpContext context) =>
{
    var filter = BuildAuditFilter(context.Request.Query);
    var rows = await auditLogService.QueryAsync(filter);
    var fields = AuditCsvBuilder.ParseFields(context.Request.Query["fields"]);
    var csv = AuditCsvBuilder.Build(rows, fields);
    return Results.File(
        Encoding.UTF8.GetBytes(csv),
        "text/csv; charset=utf-8",
        $"audit-events-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
})
    .RequireAuthorization(PermissionPolicies.AuditRead);

app.MapPost("/api/audit/exports", async (CreateAuditExportRequest request, AuditExportService auditExportService, HttpContext context) =>
{
    var requester = GetRequester(context);
    var filter = request.Filter ?? new AuditQueryFilter();
    var fields = request.Fields ?? [];
    var callbackUrl = ParseHttpUri(request.CallbackUrl);
    var downloadUrl = callbackUrl is null
        ? null
        : $"{context.Request.Scheme}://{context.Request.Host}/api/audit/exports/{{jobId}}/download";
    var job = await auditExportService.CreateJobAsync(requester, filter, fields, callbackUrl, downloadUrl);
    return Results.Ok(AppResult<AuditExportJobDto>.Ok(ToAuditExportJobDto(job), context.TraceIdentifier));
}).RequireAuthorization(PermissionPolicies.AuditRead);

app.MapGet("/api/audit/exports", async (AuditExportService auditExportService, HttpContext context) =>
{
    var requester = GetRequester(context);
    var query = context.Request.Query;
    var filter = new AuditExportJobQueryFilter(
        Status: query["status"],
        From: ParseDate(query["from"]),
        To: ParseDate(query["to"]),
        Page: ParseInt(query["page"]),
        PageSize: ParseInt(query["pageSize"]));
    var result = await auditExportService.QueryJobsAsync(requester, filter);
    return Results.Ok(AppResult<AuditExportJobPageDto>.Ok(ToAuditExportJobPageDto(result), context.TraceIdentifier));
}).RequireAuthorization(PermissionPolicies.AuditRead);

app.MapGet("/api/audit/exports/{jobId}", async (string jobId, AuditExportService auditExportService, HttpContext context) =>
{
    var requester = GetRequester(context);
    if (!Guid.TryParse(jobId, out var id))
    {
        throw new AppException(ErrorCodes.NotFound, "导出任务不存在。");
    }

    var job = await auditExportService.GetJobAsync(id, requester);
    if (job is null)
    {
        throw new AppException(ErrorCodes.NotFound, "导出任务不存在。");
    }

    return Results.Ok(AppResult<AuditExportJobDto>.Ok(ToAuditExportJobDto(job), context.TraceIdentifier));
}).RequireAuthorization(PermissionPolicies.AuditRead);

app.MapGet("/api/audit/exports/{jobId}/download", async (string jobId, AuditExportService auditExportService, HttpContext context) =>
{
    var requester = GetRequester(context);
    if (!Guid.TryParse(jobId, out var id))
    {
        throw new AppException(ErrorCodes.NotFound, "导出任务不存在。");
    }

    var csv = await auditExportService.GetCompletedCsvAsync(id, requester);
    if (string.IsNullOrEmpty(csv))
    {
        throw new AppException(ErrorCodes.ValidationError, "导出任务尚未完成。");
    }

    return Results.File(
        Encoding.UTF8.GetBytes(csv),
        "text/csv; charset=utf-8",
        $"audit-export-{id:N}.csv");
}).RequireAuthorization(PermissionPolicies.AuditRead);

app.MapGet("/api/modules/contracts", (IReadOnlyCollection<IBusinessModule> modules, HttpContext context) =>
{
    var payload = modules.Select(x => new
    {
        x.Metadata.ModuleCode,
        x.Metadata.ModuleName,
        x.Metadata.ModuleVersion,
        Permissions = x.GetPermissions(),
        Menus = x.GetMenus(),
        Audits = x.GetAuditDeclarations()
    });

    return Results.Ok(AppResult<object>.Ok(payload, context.TraceIdentifier));
});
app.MapPost("/api/modules/contracts/validate", (ValidateModuleContractsRequest? request, IReadOnlyCollection<IBusinessModule> modules, HttpContext context) =>
{
    var protocolVersion = request?.ProtocolVersion;
    var result = BuildModuleContractValidationResult(modules, protocolVersion);
    return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
});
app.MapGet("/api/modules/contracts/report", (string? protocolVersion, string? format, IReadOnlyCollection<IBusinessModule> modules, HttpContext context) =>
{
    var report = BuildModuleContractValidationResult(modules, protocolVersion);
    if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
    {
        var csv = BuildModuleContractValidationCsv(report);
        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv; charset=utf-8",
            $"module-contract-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    return Results.Ok(AppResult<object>.Ok(report, context.TraceIdentifier));
});

app.MapGet("/api/modules/contracts/report/download", (string? protocolVersion, IReadOnlyCollection<IBusinessModule> modules) =>
{
    var report = BuildModuleContractValidationResult(modules, protocolVersion);
    var csv = BuildModuleContractValidationCsv(report);
    return Results.File(
        Encoding.UTF8.GetBytes(csv),
        "text/csv; charset=utf-8",
        $"module-contract-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
});

object BuildModuleContractValidationResult(IReadOnlyCollection<IBusinessModule> modules, string? protocolVersion)
{
    var errors = new List<string>();
    var warnings = new List<string>();
    var seenModuleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var seenMenuCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var moduleResults = new List<object>();
    var validAuditLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

    foreach (var module in modules.OrderBy(x => x.Metadata.ModuleCode, StringComparer.OrdinalIgnoreCase))
    {
        var metadata = module.Metadata;
        var moduleErrors = new List<string>();
        var moduleWarnings = new List<string>();
        var moduleCode = metadata.ModuleCode?.Trim() ?? string.Empty;
        var moduleVersion = metadata.ModuleVersion?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(moduleCode))
        {
            moduleErrors.Add("moduleCode 不能为空。");
        }
        else if (!Regex.IsMatch(moduleCode, "^[a-z][a-z0-9_-]{1,63}$"))
        {
            moduleErrors.Add($"moduleCode 格式不符合约定: {moduleCode}");
        }

        if (!string.IsNullOrWhiteSpace(moduleCode) && !seenModuleCodes.Add(moduleCode))
        {
            moduleErrors.Add($"moduleCode 重复: {moduleCode}");
        }

        if (string.IsNullOrWhiteSpace(moduleVersion))
        {
            moduleErrors.Add("moduleVersion 不能为空。");
        }
        else if (!Regex.IsMatch(moduleVersion, "^\\d+\\.\\d+\\.\\d+(-[0-9A-Za-z.-]+)?$"))
        {
            moduleErrors.Add($"moduleVersion 非语义化版本: {moduleVersion}");
        }

        if (!string.IsNullOrWhiteSpace(protocolVersion) &&
            TryGetSemVerMajor(moduleVersion, out var moduleMajor) &&
            TryGetSemVerMajor(protocolVersion, out var protocolMajor) &&
            moduleMajor != protocolMajor)
        {
            moduleWarnings.Add($"模块主版本({moduleMajor})与协议主版本({protocolMajor})不一致。");
        }

        var permissions = module.GetPermissions();
        var permissionCodes = permissions.Select(x => x.PermissionCode).Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicatePermissionCodes = permissions
            .GroupBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        if (duplicatePermissionCodes.Length > 0)
        {
            moduleErrors.Add($"权限点重复: {string.Join(", ", duplicatePermissionCodes)}");
        }

        foreach (var menu in module.GetMenus())
        {
            if (string.IsNullOrWhiteSpace(menu.MenuCode))
            {
                moduleErrors.Add("存在 menuCode 为空的菜单声明。");
                continue;
            }
            if (!seenMenuCodes.Add(menu.MenuCode))
            {
                moduleErrors.Add($"menuCode 重复: {menu.MenuCode}");
            }
            if (string.IsNullOrWhiteSpace(menu.PermissionCode))
            {
                moduleWarnings.Add($"菜单 {menu.MenuCode} 未声明 permissionCode。");
            }
            else if (!permissionCodes.Contains(menu.PermissionCode))
            {
                moduleWarnings.Add($"菜单 {menu.MenuCode} 绑定了未声明权限点: {menu.PermissionCode}");
            }
        }

        foreach (var audit in module.GetAuditDeclarations())
        {
            if (!validAuditLevels.Contains(audit.Level))
            {
                moduleWarnings.Add($"审计事件 {audit.EventCode} 使用了非常规 level: {audit.Level}");
            }
        }

        errors.AddRange(moduleErrors.Select(x => $"[{moduleCode}] {x}"));
        warnings.AddRange(moduleWarnings.Select(x => $"[{moduleCode}] {x}"));
        moduleResults.Add(new
        {
            metadata.ModuleCode,
            metadata.ModuleName,
            metadata.ModuleVersion,
            IsValid = moduleErrors.Count == 0,
            ErrorCount = moduleErrors.Count,
            WarningCount = moduleWarnings.Count,
            Errors = moduleErrors,
            Warnings = moduleWarnings
        });
    }

    return new
    {
        IsValid = errors.Count == 0,
        ModuleCount = moduleResults.Count,
        ErrorCount = errors.Count,
        WarningCount = warnings.Count,
        Errors = errors,
        Warnings = warnings,
        Modules = moduleResults
    };
}

app.MapGet("/api/tenants", async (TenantService tenantService, HttpContext context) =>
{
    var data = await tenantService.GetTenantsAsync();
    var payload = data.Select(x => new { x.TenantId, x.TenantName, x.Enabled, x.CreatedAt }).ToArray();
    return Results.Ok(AppResult<object>.Ok(payload, context.TraceIdentifier));
});
app.MapPost("/api/tenants", async (CreateTenantRequest request, TenantService tenantService, HttpContext context) =>
{
    var tenant = await tenantService.CreateTenantAsync(request.TenantId, request.TenantName);
    return Results.Ok(AppResult<object>.Ok(new { tenant.TenantId, tenant.TenantName, tenant.Enabled, tenant.CreatedAt }, context.TraceIdentifier));
});
app.MapGet("/api/tenants/{tenantId}/settings", async (string tenantId, TenantService tenantService, HttpContext context) =>
{
    var settings = await tenantService.GetSettingsAsync(tenantId);
    return Results.Ok(AppResult<IReadOnlyDictionary<string, string>>.Ok(settings, context.TraceIdentifier));
});
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
});

app.MapPost("/api/auth/sso/login", async (SsoLoginRequest request, ExternalIdentityLinkService linkService, UserService userService, AuthService authService, HttpContext context) =>
{
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
});
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
});
app.MapPost("/api/notifications/webhook", async (CreateWebhookNotificationRequest request, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
{
    var message = await notificationService.SendWebhookAsync(request.CallbackUrl, request.Title, request.Content);
    if (message.Status == "Failed")
    {
        await jobSchedulingService.EnqueueWebhookRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel, message.Error }, context.TraceIdentifier));
});
app.MapPost("/api/notifications/email", async (CreateEmailNotificationRequest request, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
{
    var message = await notificationService.SendEmailAsync(request.ReceiverEmail, request.Title, request.Content);
    if (message.Status == "Failed")
    {
        await jobSchedulingService.EnqueueEmailRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
    }
    return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel, message.Error }, context.TraceIdentifier));
});
app.MapPost("/api/notifications/sms", async (CreateSmsNotificationRequest request, NotificationService notificationService, JobSchedulingService jobSchedulingService, HttpContext context) =>
{
    var message = await notificationService.SendSmsAsync(request.ReceiverPhone, request.Title, request.Content);
    if (message.Status == "Failed")
    {
        await jobSchedulingService.EnqueueSmsRetryAsync(message.NotificationMessageId, DateTimeOffset.UtcNow.AddMinutes(1));
    }
    return Results.Ok(AppResult<object>.Ok(new { message.NotificationMessageId, message.Status, message.Channel, message.Error }, context.TraceIdentifier));
});
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
});
app.MapPost("/api/notifications/templates/{templateCode}/versions", async (string templateCode, PublishNotificationTemplateVersionRequest request, NotificationTemplateService templateService, HttpContext context) =>
{
    var changedBy = GetOptionalRequester(context);
    var result = await templateService.PublishVersionAsync(templateCode, request.Title, request.Content, changedBy);
    return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
});
app.MapGet("/api/notifications/templates", async (NotificationTemplateService templateService, HttpContext context) =>
{
    var templates = await templateService.GetTemplatesAsync();
    return Results.Ok(AppResult<object>.Ok(templates, context.TraceIdentifier));
});
app.MapGet("/api/notifications/templates/{templateCode}", async (string templateCode, int? version, NotificationTemplateService templateService, HttpContext context) =>
{
    var template = await templateService.GetTemplateAsync(templateCode, version);
    if (template is null)
    {
        throw new AppException(ErrorCodes.NotFound, "模板不存在。", 404);
    }
    return Results.Ok(AppResult<object>.Ok(template, context.TraceIdentifier));
});
app.MapPost("/api/notifications/templates/{templateCode}/enabled", async (string templateCode, SetNotificationTemplateEnabledRequest request, NotificationTemplateService templateService, HttpContext context) =>
{
    var changedBy = GetOptionalRequester(context);
    var template = await templateService.SetEnabledAsync(templateCode, request.Enabled, changedBy);
    return Results.Ok(AppResult<object>.Ok(template, context.TraceIdentifier));
});
app.MapGet("/api/notifications/templates/{templateCode}/versions", async (string templateCode, int? take, NotificationTemplateService templateService, HttpContext context) =>
{
    var versions = await templateService.GetVersionsAsync(templateCode, take ?? 20);
    return Results.Ok(AppResult<object>.Ok(versions, context.TraceIdentifier));
});
app.MapGet("/api/notifications/templates/{templateCode}/diff", async (string templateCode, int fromVersion, int toVersion, NotificationTemplateService templateService, HttpContext context) =>
{
    var diff = await templateService.DiffVersionsAsync(templateCode, fromVersion, toVersion);
    return Results.Ok(AppResult<object>.Ok(diff, context.TraceIdentifier));
});
app.MapGet("/api/notifications/templates/{templateCode}/variables", async (string templateCode, int? version, NotificationTemplateService templateService, HttpContext context) =>
{
    var variables = await templateService.GetVariablesAsync(templateCode, version);
    return Results.Ok(AppResult<object>.Ok(new { templateCode, version, variables }, context.TraceIdentifier));
});
app.MapPost("/api/notifications/templates/{templateCode}/preview", async (string templateCode, PreviewNotificationTemplateRequest request, NotificationTemplateService templateService, HttpContext context) =>
{
    var rendered = await templateService.RenderAsync(templateCode, request.Version, request.Variables, allowDisabledTemplate: request.AllowDisabledTemplate);
    return Results.Ok(AppResult<object>.Ok(rendered, context.TraceIdentifier));
});
app.MapPost("/api/notifications/templates/{templateCode}/rollback", async (string templateCode, RollbackNotificationTemplateRequest request, NotificationTemplateService templateService, HttpContext context) =>
{
    var changedBy = GetOptionalRequester(context);
    var result = await templateService.RollbackAsync(templateCode, request.TargetVersion, changedBy);
    return Results.Ok(AppResult<object>.Ok(result, context.TraceIdentifier));
});
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
});

app.MapPost("/api/scheduling/webhook-retry", async (CreateWebhookRetryJobRequest request, JobSchedulingService jobSchedulingService, HttpContext context) =>
{
    var job = await jobSchedulingService.EnqueueWebhookRetryAsync(request.NotificationMessageId, request.RunAt ?? DateTimeOffset.UtcNow.AddMinutes(1));
    return Results.Ok(AppResult<object>.Ok(new { job.ScheduledJobId, job.JobType, job.Status, job.RunAt }, context.TraceIdentifier));
});
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
});

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

app.MapGroup("/api/modules").WithTags("Modules");
foreach (var module in discoveredModules)
{
    module.MapEndpoints(app);
}

app.Run();

static AuditQueryFilter BuildAuditFilter(IQueryCollection query)
{
    return new AuditQueryFilter(
        From: ParseDate(query["from"]),
        To: ParseDate(query["to"]),
        RequestPath: query["requestPath"],
        HttpMethod: query["httpMethod"],
        StatusCode: ParseInt(query["statusCode"]),
        Actor: query["actor"],
        EventCode: query["eventCode"],
        Level: query["level"],
        TraceId: query["traceId"],
        Limit: ParseInt(query["limit"]),
        Page: ParseInt(query["page"]),
        PageSize: ParseInt(query["pageSize"]),
        Sort: query["sort"]);
}

static DateTimeOffset? ParseDate(string? input) =>
    DateTimeOffset.TryParse(input, out var value) ? value : null;

static int? ParseInt(string? input) =>
    int.TryParse(input, out var value) ? value : null;

static Uri? ParseHttpUri(string? input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return null;
    }

    if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        throw new AppException(ErrorCodes.ValidationError, "callbackUrl 必须是合法的 http/https 绝对地址。");
    }

    return uri;
}

static string GetRequester(HttpContext? context)
{
    var requester = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(requester))
    {
        throw new AppException(ErrorCodes.Unauthorized, "未识别到登录用户。", StatusCodes.Status401Unauthorized);
    }

    return requester;
}

static string GetOptionalRequester(HttpContext? context)
{
    var requester = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return string.IsNullOrWhiteSpace(requester) ? "system" : requester;
}

static AuditExportJobDto ToAuditExportJobDto(AuditExportJobInfo job) =>
    new(job.JobId, job.CreatedBy, job.Status, job.CreatedAt, job.CompletedAt, job.Error);

static AuditExportJobPageDto ToAuditExportJobPageDto(AuditExportJobPageResult page) =>
    new(page.Items.Select(ToAuditExportJobDto).ToArray(), page.Total, page.Page, page.PageSize);

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.ToDictionary(
            x => x.Key,
            x => new
            {
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                durationMs = x.Value.Duration.TotalMilliseconds
            })
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

static bool TryGetSemVerMajor(string version, out int major)
{
    major = 0;
    if (string.IsNullOrWhiteSpace(version))
    {
        return false;
    }

    var input = version.Trim();
    var separator = input.IndexOfAny(['.', '-']);
    var majorText = separator >= 0 ? input[..separator] : input;
    return int.TryParse(majorText, out major);
}

static string BuildModuleContractValidationCsv(object report)
{
    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(report));
    var root = doc.RootElement;
    var modulesElement = root.TryGetProperty("modules", out var camelModules)
        ? camelModules
        : root.GetProperty("Modules");
    var lines = new List<string>
    {
        "\"moduleCode\",\"moduleName\",\"moduleVersion\",\"isValid\",\"errorCount\",\"warningCount\",\"errors\",\"warnings\""
    };

    foreach (var module in modulesElement.EnumerateArray())
    {
        var errors = string.Join(" | ", GetPropertyCaseInsensitive(module, "errors").EnumerateArray().Select(x => x.GetString() ?? string.Empty));
        var warnings = string.Join(" | ", GetPropertyCaseInsensitive(module, "warnings").EnumerateArray().Select(x => x.GetString() ?? string.Empty));
        lines.Add(string.Join(",",
            EscapeCsv(GetPropertyCaseInsensitive(module, "moduleCode").GetString()),
            EscapeCsv(GetPropertyCaseInsensitive(module, "moduleName").GetString()),
            EscapeCsv(GetPropertyCaseInsensitive(module, "moduleVersion").GetString()),
            EscapeCsv(GetPropertyCaseInsensitive(module, "isValid").GetBoolean().ToString()),
            EscapeCsv(GetPropertyCaseInsensitive(module, "errorCount").GetInt32().ToString()),
            EscapeCsv(GetPropertyCaseInsensitive(module, "warningCount").GetInt32().ToString()),
            EscapeCsv(errors),
            EscapeCsv(warnings)));
    }

    return string.Join("\n", lines);
}

static string EscapeCsv(string? input)
{
    var value = input ?? string.Empty;
    return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

static JsonElement GetPropertyCaseInsensitive(JsonElement obj, string name)
{
    if (obj.TryGetProperty(name, out var value))
    {
        return value;
    }

    var pascal = char.ToUpperInvariant(name[0]) + name[1..];
    if (obj.TryGetProperty(pascal, out value))
    {
        return value;
    }

    throw new KeyNotFoundException($"Property not found: {name}");
}

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
internal sealed record AuditExportJobDto(Guid JobId, string CreatedBy, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? Error);
internal sealed record AuditExportJobPageDto(IReadOnlyCollection<AuditExportJobDto> Items, int Total, int Page, int PageSize);

public partial class Program;
