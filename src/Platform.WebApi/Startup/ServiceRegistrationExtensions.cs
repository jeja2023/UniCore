using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform.AuditLog.Services;
using Platform.Auth.Services;
using Platform.Core.Abstractions;
using Platform.Identity.Services;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Services;
using Platform.Module.Abstractions.Contracts;
using Platform.Permission.Services;
using Platform.WebApi.Auth;
using Platform.WebApi.Health;
using Platform.WebApi.Metrics;
using Platform.WebApi.Options;
using Platform.WebApi.OpenApi;
using Platform.AuditLog.Metrics;

namespace Platform.WebApi.Startup;

internal static class ServiceRegistrationExtensions
{
    private static readonly string[] SkippedFrameworkAssemblyPrefixes =
    [
        "Microsoft.",
        "System.",
        "mscorlib",
        "netstandard",
        "Npgsql",
        "Mono.",
        "SQLitePCLRaw",
        "Swashbuckle",
        "AWSSDK",
        "HealthChecks",
        "Humanizer",
        "YamlDotNet",
        "Hangfire",
        "OpenTelemetry",
        "Grpc",
        "Google.",
        "Polly.",
        "Serilog",
        "AutoMapper",
        "FluentValidation",
        "StackExchange.",
        "Pipelines.",
        "IdentityModel",
        "NuGet.",
        "Castle.",
        "FluentAssertions",
        "Moq.",
        "coverlet."
    ];

    internal static void AddPlatformWebApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var useInMemoryDatabase = configuration.GetValue<bool>("UseInMemoryDatabase");
        ValidateProductionSecurityBaselines(configuration);
        services.AddEndpointsApiExplorer();
        services.AddMemoryCache();
        services.AddSingleton<RequestMetricsStore>();
        services.AddSingleton<AuditExportMetricsStore>();
        services.AddSingleton<AuditLogWriteMetricsStore>();
        services.AddSwaggerGen(options =>
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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .PostConfigure(options =>
            {
                if (useInMemoryDatabase && string.IsNullOrWhiteSpace(options.SigningKey))
                {
                    options.SigningKey = "IntegrationTests_Only_Signing_Key_At_Least_32_Chars";
                }
            })
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "Jwt:Issuer 不能为空。")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "Jwt:Audience 不能为空。")
            .Validate(x => !string.IsNullOrWhiteSpace(x.SigningKey), "Jwt:SigningKey 不能为空。")
            .Validate(x => x.SigningKey.Length >= 32, "Jwt:SigningKey 至少 32 个字符。")
            .Validate(x => x.UserEnabledCacheSeconds >= 0 && x.UserEnabledCacheSeconds <= 3600, "Jwt:UserEnabledCacheSeconds 必须在 0~3600 之间（0 表示禁用缓存）。")
            .ValidateOnStart();
        services.Configure<LoginSecurityOptions>(configuration.GetSection(LoginSecurityOptions.Section));

        services.AddOptions<RequestAuditOptions>()
            .Bind(configuration.GetSection(RequestAuditOptions.Section))
            .Validate(
                x => x.SamplingPercent is >= 0 and <= 100,
                "RequestAudit:SamplingPercent 必须在 0~100 之间（0 表示不写 HTTP 请求审计）。")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (useInMemoryDatabase)
            {
                options.UseInMemoryDatabase("unicore-test-db");
                return;
            }

            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Default 未配置。");
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.UseNpgsql(connectionString);
            options.ReplaceService<IHistoryRepository, CommentedNpgsqlHistoryRepository>();
        });

        var jwtOptions = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (useInMemoryDatabase && string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            jwtOptions.SigningKey = "IntegrationTests_Only_Signing_Key_At_Least_32_Chars";
        }
        services
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
                            return;
                        }

                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var tenantId = context.Principal?.FindFirstValue("tenant_id");
                        if (!Guid.TryParse(userId, out var parsedUserId) || string.IsNullOrWhiteSpace(tenantId))
                        {
                            context.Fail("token user invalid");
                            return;
                        }

                        var jwtRuntimeOptions = context.HttpContext.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value;
                        var enabledCache = context.HttpContext.RequestServices.GetRequiredService<IJwtUserEnabledValidationCache>();
                        if (jwtRuntimeOptions.UserEnabledCacheSeconds > 0 &&
                            enabledCache.TryGet(parsedUserId, tenantId, out var cachedEnabled))
                        {
                            if (!cachedEnabled)
                            {
                                context.Fail("user disabled");
                            }

                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var userEnabled = await dbContext.Users
                            .AsNoTracking()
                            .AnyAsync(
                                x => x.UserId == parsedUserId &&
                                     x.TenantId == tenantId &&
                                     x.Enabled,
                                context.HttpContext.RequestAborted);
                        if (!userEnabled)
                        {
                            context.Fail("user disabled");
                            return;
                        }

                        if (jwtRuntimeOptions.UserEnabledCacheSeconds > 0)
                        {
                            enabledCache.Set(
                                parsedUserId,
                                tenantId,
                                true,
                                TimeSpan.FromSeconds(jwtRuntimeOptions.UserEnabledCacheSeconds));
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            PermissionCatalog.RegisterAuthorizationPolicies(options);
        });
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CurrentUserContextService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<PermissionVersionService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<AuditExportService>();
        services.AddScoped<TenantService>();
        services.AddScoped<ExternalIdentityLinkService>();
        services.AddScoped<DataScopeService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationTemplateService>();
        services.AddScoped<JobSchedulingService>();
        services.AddScoped<OidcSsoService>();
        services.AddScoped<DictionaryService>();
        services.AddScoped<EntityChangeAuditService>();

        services.AddOptions<RedisCacheOptions>()
            .Bind(configuration.GetSection(RedisCacheOptions.Section))
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.ConnectionString), "RedisCache 启用时必须提供 ConnectionString。")
            .ValidateOnStart();
        var redisOptions = configuration.GetSection(RedisCacheOptions.Section).Get<RedisCacheOptions>() ?? new RedisCacheOptions();
        if (redisOptions.Enabled && !string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisOptions.ConnectionString);
            services.AddSingleton<IAppCache, DistributedAppCache>();
            services.AddSingleton<IJwtUserEnabledValidationCache, DistributedJwtUserEnabledValidationCache>();
        }
        else
        {
            services.AddSingleton<IAppCache, InMemoryAppCache>();
            services.AddSingleton<IJwtUserEnabledValidationCache, JwtUserEnabledValidationCache>();
        }

        services.AddSingleton<IAppEventBus, InMemoryAppEventBus>();

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();

        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.Section))
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.Endpoint), "ObjectStorage 启用时必须配置 Endpoint。")
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.AccessKey), "ObjectStorage 启用时必须配置 AccessKey。")
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.SecretKey), "ObjectStorage 启用时必须配置 SecretKey。")
            .ValidateOnStart();
        var objectStorageOptions = configuration.GetSection(ObjectStorageOptions.Section).Get<ObjectStorageOptions>() ?? new ObjectStorageOptions();
        ValidateObjectStorageOptions(objectStorageOptions);
        services.Configure<LocalFileStorageOptions>(configuration.GetSection(LocalFileStorageOptions.Section));
        services.AddScoped<IFileStorage>(sp =>
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

        services.AddHttpClient(nameof(NotificationService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient(nameof(AuditExportService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient(nameof(OidcSsoService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services
            .AddHealthChecks()
            .AddCheck<AppDbContextHealthCheck>("database", failureStatus: HealthStatus.Unhealthy);

        AddOpenTelemetryTracing(services, configuration);

        services.AddOptions<AuditExportCallbackOptions>()
            .Bind(configuration.GetSection(AuditExportCallbackOptions.Section))
            .Validate(x => string.IsNullOrWhiteSpace(x.SigningKey) || x.SigningKey.Length >= 16, "AuditExportCallback:SigningKey 为空或至少 16 个字符。")
            .ValidateOnStart();
        services.AddOptions<AuditExportCleanupOptions>()
            .Bind(configuration.GetSection(AuditExportCleanupOptions.Section))
            .Validate(x => x.Ttl > TimeSpan.Zero, "AuditExportCleanup:Ttl 必须大于 0。")
            .Validate(x => x.RunInterval > TimeSpan.Zero, "AuditExportCleanup:RunInterval 必须大于 0。")
            .ValidateOnStart();
        services.AddOptions<JobSchedulingOptions>()
            .Bind(configuration.GetSection(JobSchedulingOptions.Section))
            .Validate(x => x.PollInterval > TimeSpan.Zero, "JobScheduling:PollInterval 必须大于 0。")
            .ValidateOnStart();
        services.AddOptions<EmailChannelOptions>()
            .Bind(configuration.GetSection(EmailChannelOptions.Section))
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.Host), "Email 启用时必须配置 Host。")
            .Validate(x => !x.Enabled || x.Port > 0, "Email 启用时必须配置有效 Port。")
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.FromAddress), "Email 启用时必须配置 FromAddress。")
            .ValidateOnStart();
        services.AddOptions<SmsChannelOptions>()
            .Bind(configuration.GetSection(SmsChannelOptions.Section))
            .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.ProviderUrl), "Sms 启用时必须配置 ProviderUrl。")
            .ValidateOnStart();
        services.AddOptions<OidcSsoOptions>()
            .Bind(configuration.GetSection(OidcSsoOptions.Section))
            .Validate(
                x => !x.Enabled || x.Providers.All(provider =>
                    !string.IsNullOrWhiteSpace(provider.Name) &&
                    !string.IsNullOrWhiteSpace(provider.TokenEndpoint) &&
                    !string.IsNullOrWhiteSpace(provider.UserInfoEndpoint) &&
                    !string.IsNullOrWhiteSpace(provider.ClientId)),
                "OIDC 启用时，Providers 需配置 Name/TokenEndpoint/UserInfoEndpoint/ClientId。")
            .ValidateOnStart();
        services.AddSingleton<IAuditLogWriteQueue, AuditLogWriteQueue>();
        services.AddHostedService<AuditExportCleanupHostedService>();
        services.AddHostedService<AuditExportJobProcessorHostedService>();
        services.AddHostedService<AuditLogWriteHostedService>();
        services.AddHostedService<JobSchedulerHostedService>();
    }

    private static void AddOpenTelemetryTracing(IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration.GetValue<string>("Telemetry:ServiceName");
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            serviceName = "UniCore.WebApi";
        }

        var serviceVersion = configuration.GetValue<string>("Telemetry:ServiceVersion");
        if (string.IsNullOrWhiteSpace(serviceVersion))
        {
            serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        }

        var enableOtlpExporter = configuration.GetValue("Telemetry:EnableOtlpExporter", false);
        var otlpEndpoint = configuration.GetValue<string>("Telemetry:Otlp:Endpoint");

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (enableOtlpExporter && !string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });
    }

    private static void ValidateProductionSecurityBaselines(IConfiguration configuration)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isProductionLike = string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment, "Staging", StringComparison.OrdinalIgnoreCase);
        if (!isProductionLike)
        {
            return;
        }

        var signingKey = configuration.GetValue<string>("Jwt:SigningKey") ?? string.Empty;
        if (IsKnownInsecureSecret(signingKey))
        {
            throw new InvalidOperationException("生产/预发环境禁止使用默认 Jwt:SigningKey，请通过安全密钥管理注入。");
        }

        var adminPassword = Environment.GetEnvironmentVariable("UNICORE_ADMIN_PASSWORD")
            ?? configuration.GetValue<string>("Seed:AdminPassword")
            ?? string.Empty;
        if (IsKnownInsecureSecret(adminPassword))
        {
            throw new InvalidOperationException("生产/预发环境禁止使用默认 Seed:AdminPassword，请通过安全密钥管理注入。");
        }
    }

    private static bool IsKnownInsecureSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        return normalized.Equals("change_me", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("changeme", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("password", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("UniCore@123", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("UniCore_Local_Dev_Signing_Key_2026_Only", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateObjectStorageOptions(ObjectStorageOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("ObjectStorage:Enabled=true 时必须配置 ObjectStorage:Endpoint。");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException("ObjectStorage:Enabled=true 时必须配置 AccessKey 与 SecretKey。");
        }
    }

    internal static IReadOnlyCollection<IBusinessModule> AddDiscoveredBusinessModules(this IServiceCollection services, Assembly entryAssembly)
    {
        var candidateAssemblies = new List<Assembly> { entryAssembly };
        var seenAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            entryAssembly.GetName().Name ?? string.Empty
        };

        foreach (var referencedAssemblyName in entryAssembly.GetReferencedAssemblies())
        {
            try
            {
                var refSimpleName = referencedAssemblyName.Name ?? string.Empty;
                if (!ShouldProbeDirectoryAssembly(refSimpleName))
                {
                    continue;
                }

                var assembly = Assembly.Load(referencedAssemblyName);
                var simpleName = assembly.GetName().Name ?? string.Empty;
                if (seenAssemblyNames.Add(simpleName))
                {
                    candidateAssemblies.Add(assembly);
                }
            }
            catch
            {
                // 在模块发现阶段忽略无法加载的程序集。
            }
        }

        foreach (var assemblyPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                var simpleName = assemblyName.Name ?? string.Empty;
                if (!ShouldProbeDirectoryAssembly(simpleName))
                {
                    continue;
                }

                if (!seenAssemblyNames.Add(simpleName))
                {
                    continue;
                }

                candidateAssemblies.Add(Assembly.Load(assemblyName));
            }
            catch
            {
                // 忽略非托管或无法加载的程序集。
            }
        }

        var discoveredModules = candidateAssemblies
            .SelectMany(x => x.GetTypes())
            .Where(t => typeof(IBusinessModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (IBusinessModule)Activator.CreateInstance(t)!)
            .GroupBy(module => module.Metadata.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        foreach (var module in discoveredModules)
        {
            module.RegisterServices(services);
        }

        services.AddSingleton<IReadOnlyCollection<IBusinessModule>>(discoveredModules);
        return discoveredModules;
    }

    /// <summary>跳过框架与平台核心程序集，减少启动时无谓的反射与加载。</summary>
    private static bool ShouldProbeDirectoryAssembly(string simpleName)
    {
        if (string.IsNullOrEmpty(simpleName))
        {
            return false;
        }

        if (simpleName.Equals("testhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (simpleName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in SkippedFrameworkAssemblyPrefixes)
        {
            if (simpleName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (simpleName.StartsWith("Platform.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
