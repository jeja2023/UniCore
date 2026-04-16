using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.AuditLog.Services;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Xunit;

namespace Platform.WebApi.IntegrationTests;

public sealed class AuthAndRbacFlowTests
{
    [Fact]
    public async Task PlatformFoundationEndpoints_ShouldWork()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var featuresResponse = await client.GetAsync("/api/platform/config/features");
        featuresResponse.EnsureSuccessStatusCode();
        var featuresBody = await featuresResponse.Content.ReadAsStringAsync();
        Assert.Contains("AuditExportCallbackEnabled", featuresBody, StringComparison.Ordinal);

        var setCache = await client.PostAsJsonAsync("/api/platform/cache/demo-key", new { Value = "demo-value", TtlSeconds = 60 });
        setCache.EnsureSuccessStatusCode();
        var getCache = await client.GetAsync("/api/platform/cache/demo-key");
        getCache.EnsureSuccessStatusCode();
        var getCacheBody = await getCache.Content.ReadAsStringAsync();
        Assert.Contains("demo-value", getCacheBody, StringComparison.Ordinal);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("unicore-file-test")), "file", "demo.txt");
        var upload = await client.PostAsync("/api/platform/files/upload", multipart);
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var fileId = uploadJson.RootElement.GetProperty("data").GetProperty("fileId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(fileId));
        var download = await client.GetAsync($"/api/platform/files/{fileId}");
        download.EnsureSuccessStatusCode();
        var downloaded = await download.Content.ReadAsStringAsync();
        Assert.Equal("unicore-file-test", downloaded);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeRequestMetrics()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();

        var metrics = await client.GetAsync("/metrics");
        metrics.EnsureSuccessStatusCode();
        var contentType = metrics.Content.Headers.ContentType?.MediaType;
        Assert.Equal("text/plain", contentType);
        var body = await metrics.Content.ReadAsStringAsync();
        Assert.Contains("unicore_http_requests_total", body, StringComparison.Ordinal);
        Assert.Contains("unicore_http_request_duration_seconds_bucket", body, StringComparison.Ordinal);
        Assert.Contains("path=\"/api/health\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpoints_ShouldReturnHealthy()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/api/health/live");
        live.EnsureSuccessStatusCode();
        var liveBody = await live.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Healthy\"", liveBody, StringComparison.Ordinal);

        var ready = await client.GetAsync("/api/health/ready");
        ready.EnsureSuccessStatusCode();
        var readyBody = await ready.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Healthy\"", readyBody, StringComparison.Ordinal);
        Assert.Contains("\"database\"", readyBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditExportCleanupHostedService_ShouldCleanupExpiredJobs()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AuditExportCleanup:Enabled"] = "true",
                        ["AuditExportCleanup:Ttl"] = "00:00:01",
                        ["AuditExportCleanup:RunInterval"] = "00:00:00.200"
                    });
                });
            });

        using var client = factory.CreateClient();
        _ = await client.GetAsync("/api/health");

        var expiredJobId = Guid.NewGuid();
        var freshJobId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditExportJobs.AddRange(
                new AuditExportJobEntity
                {
                    JobId = expiredJobId,
                    CreatedBy = "cleanup-test-user",
                    Status = "Completed",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    Completed = true,
                    CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    CsvContent = "\"header\"\n\"value\""
                },
                new AuditExportJobEntity
                {
                    JobId = freshJobId,
                    CreatedBy = "cleanup-test-user",
                    Status = "Processing",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Completed = false
                });
            await db.SaveChangesAsync();
        }

        var cleaned = await WaitForConditionAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var expiredExists = await db.AuditExportJobs.AnyAsync(x => x.JobId == expiredJobId);
            var freshExists = await db.AuditExportJobs.AnyAsync(x => x.JobId == freshJobId);
            return !expiredExists && freshExists;
        }, timeoutMs: 5000, stepMs: 100);

        Assert.True(cleaned);
    }

    [Fact]
    public async Task CreateAuditExport_WithInvalidCallbackUrl_ShouldReturnValidationError()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });

        using var client = factory.CreateClient();
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/audit/exports", new
        {
            Filter = new
            {
                HttpMethod = "GET",
                Limit = 1
            },
            Fields = new[] { "occurredAt", "eventCode" },
            CallbackUrl = "ftp://invalid-callback.local/hook"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COMMON.VALIDATION_ERROR", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAuditExport_CallbackNon2xx_ShouldNotBlockJobCompletion()
    {
        var callbackRecorder = new CallbackRecorder();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureTestServices(services =>
                {
                    services.Configure<AuditExportCallbackOptions>(options =>
                    {
                        options.SigningKey = "integration-test-signing-key";
                    });
                    services.RemoveAll<IHttpClientFactory>();
                    services.AddSingleton(callbackRecorder);
                    services.AddSingleton<IHttpClientFactory>(sp =>
                        new CallbackHttpClientFactory(
                            sp.GetRequiredService<CallbackRecorder>(),
                            HttpStatusCode.InternalServerError));
                });
            });

        using var client = factory.CreateClient();
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createJobResponse = await client.PostAsJsonAsync("/api/audit/exports", new
        {
            Filter = new
            {
                HttpMethod = "GET",
                Limit = 10
            },
            Fields = new[] { "occurredAt", "eventCode", "actor" },
            CallbackUrl = "https://callback.test.local/non-2xx"
        });
        createJobResponse.EnsureSuccessStatusCode();
        using var createJobJson = JsonDocument.Parse(await createJobResponse.Content.ReadAsStringAsync());
        var jobId = createJobJson.RootElement.GetProperty("data").GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        HttpResponseMessage? downloadResponse = null;
        for (var i = 0; i < 10; i++)
        {
            downloadResponse = await client.GetAsync($"/api/audit/exports/{jobId}/download");
            if (downloadResponse.IsSuccessStatusCode)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(downloadResponse);
        downloadResponse!.EnsureSuccessStatusCode();

        var statusResponse = await client.GetAsync($"/api/audit/exports/{jobId}");
        statusResponse.EnsureSuccessStatusCode();
        using var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("Completed", statusJson.RootElement.GetProperty("data").GetProperty("status").GetString());

        CallbackRecord? callback = null;
        for (var i = 0; i < 10; i++)
        {
            if (callbackRecorder.TryDequeue(out callback))
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(callback);
        Assert.Equal("https://callback.test.local/non-2xx", callback!.Url);
    }

    [Fact]
    public async Task NotificationSmsEndpoint_WhenEnabled_ShouldSendSuccessfully()
    {
        var callbackRecorder = new CallbackRecorder();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["NotificationChannels:Sms:Enabled"] = "true",
                        ["NotificationChannels:Sms:ProviderUrl"] = "https://sms.test.local/send",
                        ["NotificationChannels:Sms:ApiKey"] = "sms-api-key",
                        ["NotificationChannels:Sms:SenderId"] = "UniCoreTest"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IHttpClientFactory>();
                    services.AddSingleton(callbackRecorder);
                    services.AddSingleton<IHttpClientFactory>(sp =>
                        new CallbackHttpClientFactory(sp.GetRequiredService<CallbackRecorder>()));
                });
            });
        using var client = factory.CreateClient();

        var sendResponse = await client.PostAsJsonAsync("/api/notifications/sms", new
        {
            ReceiverPhone = "+8613800000000",
            Title = "验证码",
            Content = "您的验证码是 123456。"
        });
        sendResponse.EnsureSuccessStatusCode();
        using var sendJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());
        var data = sendJson.RootElement.GetProperty("data");
        Assert.Equal("Sms", data.GetProperty("channel").GetString());
        Assert.Equal("Sent", data.GetProperty("status").GetString());

        CallbackRecord? callback = null;
        for (var i = 0; i < 10; i++)
        {
            if (callbackRecorder.TryDequeue(out callback))
            {
                break;
            }
            await Task.Delay(50);
        }

        Assert.NotNull(callback);
        Assert.Equal("https://sms.test.local/send", callback!.Url);
        Assert.Equal("POST", callback.Method);
    }

    [Fact]
    public async Task NotificationTemplateEndpoints_ShouldSupportVersioningEnableAndDiff()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var publishV1 = await client.PostAsJsonAsync("/api/notifications/templates/order_created/versions", new
        {
            Title = "订单创建通知",
            Content = "订单 {{orderNo}} 已创建。"
        });
        publishV1.EnsureSuccessStatusCode();

        var publishV2 = await client.PostAsJsonAsync("/api/notifications/templates/order_created/versions", new
        {
            Title = "订单创建通知(增强)",
            Content = "订单 {{orderNo}} 已创建，金额 {{amount}}。"
        });
        publishV2.EnsureSuccessStatusCode();

        var setEnabled = await client.PostAsJsonAsync("/api/notifications/templates/order_created/enabled", new
        {
            Enabled = false
        });
        setEnabled.EnsureSuccessStatusCode();
        var setEnabledBody = await setEnabled.Content.ReadAsStringAsync();
        Assert.Contains("\"enabled\":false", setEnabledBody, StringComparison.OrdinalIgnoreCase);

        var versionsResponse = await client.GetAsync("/api/notifications/templates/order_created/versions?take=10");
        versionsResponse.EnsureSuccessStatusCode();
        using var versionsJson = JsonDocument.Parse(await versionsResponse.Content.ReadAsStringAsync());
        var versions = versionsJson.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("version").GetInt32()).OrderBy(x => x).ToArray();
        Assert.True(versions.Length >= 2);

        var getCurrent = await client.GetAsync("/api/notifications/templates/order_created");
        getCurrent.EnsureSuccessStatusCode();
        var currentBody = await getCurrent.Content.ReadAsStringAsync();
        Assert.Contains("\"enabled\":false", currentBody, StringComparison.OrdinalIgnoreCase);

        var diff = await client.GetAsync($"/api/notifications/templates/order_created/diff?fromVersion={versions[^2]}&toVersion={versions[^1]}");
        diff.EnsureSuccessStatusCode();
        var diffBody = await diff.Content.ReadAsStringAsync();
        Assert.Contains("amount", diffBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("addedTokens", diffBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotificationTemplateSendEndpoint_ShouldRenderAndSendSms()
    {
        var callbackRecorder = new CallbackRecorder();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["NotificationChannels:Sms:Enabled"] = "true",
                        ["NotificationChannels:Sms:ProviderUrl"] = "https://sms.test.local/send"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IHttpClientFactory>();
                    services.AddSingleton(callbackRecorder);
                    services.AddSingleton<IHttpClientFactory>(sp =>
                        new CallbackHttpClientFactory(sp.GetRequiredService<CallbackRecorder>()));
                });
            });
        using var client = factory.CreateClient();

        var publish = await client.PostAsJsonAsync("/api/notifications/templates/order_paid/versions", new
        {
            Title = "订单 {{orderNo}} 支付成功",
            Content = "用户 {{userName}} 已完成付款。"
        });
        publish.EnsureSuccessStatusCode();

        var send = await client.PostAsJsonAsync("/api/notifications/template-send", new
        {
            TemplateCode = "order_paid",
            Channel = "sms",
            Variables = new Dictionary<string, string>
            {
                ["orderNo"] = "A10001",
                ["userName"] = "Alice"
            },
            ReceiverPhone = "+8613900000000"
        });
        send.EnsureSuccessStatusCode();
        var sendBody = await send.Content.ReadAsStringAsync();
        Assert.Contains("A10001", sendBody, StringComparison.Ordinal);
        Assert.Contains("Alice", sendBody, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Sent\"", sendBody, StringComparison.OrdinalIgnoreCase);

        CallbackRecord? callback = null;
        for (var i = 0; i < 10; i++)
        {
            if (callbackRecorder.TryDequeue(out callback))
            {
                break;
            }
            await Task.Delay(50);
        }

        Assert.NotNull(callback);
        Assert.Equal("https://sms.test.local/send", callback!.Url);
        Assert.Contains("A10001", callback.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotificationTemplatePreviewVariablesRollbackAndMissingVariables_ShouldWork()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var publishV1 = await client.PostAsJsonAsync("/api/notifications/templates/payment_notice/versions", new
        {
            Title = "支付成功 {{orderNo}}",
            Content = "用户 {{userName}} 已支付 {{amount}} 元。"
        });
        publishV1.EnsureSuccessStatusCode();

        var publishV2 = await client.PostAsJsonAsync("/api/notifications/templates/payment_notice/versions", new
        {
            Title = "支付完成 {{orderNo}}",
            Content = "用户 {{userName}} 已完成付款，订单金额 {{amount}} 元。"
        });
        publishV2.EnsureSuccessStatusCode();

        var variablesResponse = await client.GetAsync("/api/notifications/templates/payment_notice/variables");
        variablesResponse.EnsureSuccessStatusCode();
        var variablesBody = await variablesResponse.Content.ReadAsStringAsync();
        Assert.Contains("orderNo", variablesBody, StringComparison.Ordinal);
        Assert.Contains("userName", variablesBody, StringComparison.Ordinal);
        Assert.Contains("amount", variablesBody, StringComparison.Ordinal);

        var previewResponse = await client.PostAsJsonAsync("/api/notifications/templates/payment_notice/preview", new
        {
            Variables = new Dictionary<string, string>
            {
                ["orderNo"] = "P-2026-001",
                ["userName"] = "Bob",
                ["amount"] = "88.00"
            }
        });
        previewResponse.EnsureSuccessStatusCode();
        var previewBody = await previewResponse.Content.ReadAsStringAsync();
        Assert.Contains("P-2026-001", previewBody, StringComparison.Ordinal);
        Assert.Contains("Bob", previewBody, StringComparison.Ordinal);
        Assert.Contains("88.00", previewBody, StringComparison.Ordinal);

        var sendWithMissingVariables = await client.PostAsJsonAsync("/api/notifications/template-send", new
        {
            TemplateCode = "payment_notice",
            Channel = "inbox",
            Receiver = "u001",
            Variables = new Dictionary<string, string>
            {
                ["orderNo"] = "P-2026-002"
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, sendWithMissingVariables.StatusCode);
        var missingBody = await sendWithMissingVariables.Content.ReadAsStringAsync();
        Assert.Contains("COMMON.VALIDATION_ERROR", missingBody, StringComparison.Ordinal);
        Assert.Contains("amount", missingBody, StringComparison.Ordinal);

        var rollback = await client.PostAsJsonAsync("/api/notifications/templates/payment_notice/rollback", new
        {
            TargetVersion = 1
        });
        rollback.EnsureSuccessStatusCode();
        var rollbackBody = await rollback.Content.ReadAsStringAsync();
        Assert.Contains("\"version\":3", rollbackBody, StringComparison.OrdinalIgnoreCase);

        var getCurrent = await client.GetAsync("/api/notifications/templates/payment_notice");
        getCurrent.EnsureSuccessStatusCode();
        var currentBody = await getCurrent.Content.ReadAsStringAsync();
        Assert.Contains("支付成功 {{orderNo}}", currentBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantSettingsEndpoints_ShouldUpsertAndReadSettings()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var createTenant = await client.PostAsJsonAsync("/api/tenants", new
        {
            TenantId = "tenant-ops",
            TenantName = "运维租户"
        });
        createTenant.EnsureSuccessStatusCode();

        var upsert = await client.PutAsJsonAsync("/api/tenants/tenant-ops/settings/security.passwordPolicy", new
        {
            SettingValue = "{\"minLength\":12,\"requireSpecial\":true}"
        });
        upsert.EnsureSuccessStatusCode();

        var settingsResponse = await client.GetAsync("/api/tenants/tenant-ops/settings");
        settingsResponse.EnsureSuccessStatusCode();
        using var settingsJson = JsonDocument.Parse(await settingsResponse.Content.ReadAsStringAsync());
        var value = settingsJson.RootElement
            .GetProperty("data")
            .GetProperty("security.passwordPolicy")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Contains("\"minLength\":12", value!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetRoleDataScope_WithInvalidCustomExpression_ShouldReturnValidationError()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "salary > 1000"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COMMON.VALIDATION_ERROR", body, StringComparison.Ordinal);
        Assert.Contains("customExpression", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DataScopeVisualizationEndpoints_ShouldComposeValidateAndQueryRoleScope()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var templatesResponse = await client.GetAsync("/api/permission/data-scope/templates");
        templatesResponse.EnsureSuccessStatusCode();
        var templatesBody = await templatesResponse.Content.ReadAsStringAsync();
        Assert.Contains("tenant_and_department", templatesBody, StringComparison.Ordinal);
        var metadataResponse = await client.GetAsync("/api/permission/data-scope/metadata");
        metadataResponse.EnsureSuccessStatusCode();
        var metadataBody = await metadataResponse.Content.ReadAsStringAsync();
        Assert.Contains("department_code", metadataBody, StringComparison.Ordinal);
        Assert.Contains("AND", metadataBody, StringComparison.Ordinal);

        var composeResponse = await client.PostAsJsonAsync("/api/permission/data-scope/compose", new
        {
            Rules = new object[]
            {
                new { Field = "tenant_id", Operator = "=", Value = "default", JoinWithPrevious = (string?)null, OpenGroupCount = 1, CloseGroupCount = 0 },
                new { Field = "department_code", Operator = "=", Value = "default", JoinWithPrevious = "AND", OpenGroupCount = 0, CloseGroupCount = 1 }
            }
        });
        composeResponse.EnsureSuccessStatusCode();
        using var composeJson = JsonDocument.Parse(await composeResponse.Content.ReadAsStringAsync());
        var expression = composeJson.RootElement.GetProperty("data").GetProperty("expression").GetString();
        Assert.False(string.IsNullOrWhiteSpace(expression));

        var validateResponse = await client.PostAsJsonAsync("/api/permission/data-scope/validate", new
        {
            CustomExpression = expression
        });
        validateResponse.EnsureSuccessStatusCode();

        var setResponse = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = expression
        });
        setResponse.EnsureSuccessStatusCode();

        var getRoleScope = await client.GetAsync("/api/permission/roles/admin/data-scope");
        getRoleScope.EnsureSuccessStatusCode();
        var roleScopeBody = await getRoleScope.Content.ReadAsStringAsync();
        Assert.Contains("\"scope\":\"Custom\"", roleScopeBody, StringComparison.Ordinal);
        Assert.Contains("tenant_id", roleScopeBody, StringComparison.Ordinal);

        var invalidComposeResponse = await client.PostAsJsonAsync("/api/permission/data-scope/compose", new
        {
            Rules = new object[]
            {
                new { Field = "tenant_id", Operator = "=", Value = "default", JoinWithPrevious = (string?)null, OpenGroupCount = 0, CloseGroupCount = 1 }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidComposeResponse.StatusCode);
    }

    [Fact]
    public async Task DataScopeHistoryAndRollback_ShouldRestoreTargetVersion()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var setV1 = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "tenant_id = 'default'"
        });
        setV1.EnsureSuccessStatusCode();
        var setV2 = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "tenant_id = 'default' AND department_code = 'default'"
        });
        setV2.EnsureSuccessStatusCode();

        var historyResponse = await client.GetAsync("/api/permission/roles/admin/data-scope/history?take=10");
        historyResponse.EnsureSuccessStatusCode();
        using var historyJson = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        var historyItems = historyJson.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.True(historyItems.Count >= 2);
        var targetVersion = historyItems
            .First(x => x.GetProperty("customExpression").GetString() == "tenant_id = 'default'")
            .GetProperty("version")
            .GetInt32();

        var rollbackResponse = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope/rollback", new
        {
            TargetVersion = targetVersion
        });
        rollbackResponse.EnsureSuccessStatusCode();

        var getRoleScope = await client.GetAsync("/api/permission/roles/admin/data-scope");
        getRoleScope.EnsureSuccessStatusCode();
        var roleScopeBody = await getRoleScope.Content.ReadAsStringAsync();
        Assert.Contains("tenant_id = 'default'", roleScopeBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetRoleDataScope_WithStaleExpectedRevision_ShouldReturnConflict()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var currentScopeResponse = await client.GetAsync("/api/permission/roles/admin/data-scope");
        currentScopeResponse.EnsureSuccessStatusCode();
        using var currentScopeJson = JsonDocument.Parse(await currentScopeResponse.Content.ReadAsStringAsync());
        var baseRevision = currentScopeJson.RootElement.GetProperty("data").GetProperty("revision").GetInt32();

        var firstSet = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "tenant_id = 'default'",
            ExpectedRevision = baseRevision
        });
        firstSet.EnsureSuccessStatusCode();

        var staleSet = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "tenant_id = 'default' AND department_code = 'default'",
            ExpectedRevision = baseRevision
        });
        Assert.Equal(HttpStatusCode.Conflict, staleSet.StatusCode);
        var staleBody = await staleSet.Content.ReadAsStringAsync();
        Assert.Contains("COMMON.VALIDATION_ERROR", staleBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DataScopeDiffEndpoint_ShouldReturnTokenChangesBetweenVersions()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var setV1 = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "tenant_id = 'default'",
            ExpectedRevision = 0
        });
        setV1.EnsureSuccessStatusCode();

        var roleScope = await client.GetAsync("/api/permission/roles/admin/data-scope");
        roleScope.EnsureSuccessStatusCode();
        using var roleScopeJson = JsonDocument.Parse(await roleScope.Content.ReadAsStringAsync());
        var currentRevision = roleScopeJson.RootElement.GetProperty("data").GetProperty("revision").GetInt32();

        var setV2 = await client.PostAsJsonAsync("/api/permission/roles/admin/data-scope", new
        {
            Scope = "Custom",
            CustomExpression = "tenant_id = 'default' AND department_code = 'default'",
            ExpectedRevision = currentRevision
        });
        setV2.EnsureSuccessStatusCode();

        var historyResponse = await client.GetAsync("/api/permission/roles/admin/data-scope/history?take=5");
        historyResponse.EnsureSuccessStatusCode();
        using var historyJson = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        var versions = historyJson.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("version").GetInt32()).OrderBy(x => x).ToList();
        var fromVersion = versions[^2];
        var toVersion = versions[^1];

        var diffResponse = await client.GetAsync($"/api/permission/roles/admin/data-scope/diff?fromVersion={fromVersion}&toVersion={toVersion}");
        diffResponse.EnsureSuccessStatusCode();
        var diffBody = await diffResponse.Content.ReadAsStringAsync();
        Assert.Contains("department_code", diffBody, StringComparison.Ordinal);
        Assert.Contains("addedTokens", diffBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("summary", diffBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DataScopeParseEndpoint_ShouldReturnStructuredTokens()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var parseResponse = await client.PostAsJsonAsync("/api/permission/data-scope/parse", new
        {
            CustomExpression = "(tenant_id = 'default' AND department_code = 'ops') OR user_id = 'u001'"
        });
        parseResponse.EnsureSuccessStatusCode();
        var parseBody = await parseResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"kind\":\"field\"", parseBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"kind\":\"joiner\"", parseBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"kind\":\"group\"", parseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModuleContractsValidateEndpoint_ShouldReturnValidationReport()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/modules/contracts/validate", new
        {
            ProtocolVersion = "1.0.0"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"moduleCount\":", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"modules\":", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"isValid\":", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModuleContractsReportEndpoint_ShouldSupportJsonAndCsv()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var jsonReport = await client.GetAsync("/api/modules/contracts/report?protocolVersion=1.0.0");
        jsonReport.EnsureSuccessStatusCode();
        var jsonBody = await jsonReport.Content.ReadAsStringAsync();
        Assert.Contains("\"moduleCount\":", jsonBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"modules\":", jsonBody, StringComparison.OrdinalIgnoreCase);

        var csvReport = await client.GetAsync("/api/modules/contracts/report?protocolVersion=1.0.0&format=csv");
        csvReport.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csvReport.Content.Headers.ContentType?.MediaType);
        var csvBody = await csvReport.Content.ReadAsStringAsync();
        Assert.Contains("\"moduleCode\",\"moduleName\",\"moduleVersion\"", csvBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Refresh_And_Rbac_WriteFlow_ShouldWork()
    {
        var callbackRecorder = new CallbackRecorder();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IHttpClientFactory>();
                    services.AddSingleton(callbackRecorder);
                    services.AddSingleton<IHttpClientFactory>(sp =>
                        new CallbackHttpClientFactory(sp.GetRequiredService<CallbackRecorder>()));
                });
            });

        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "admin",
            Password = "UniCore@123"
        });
        loginResponse.EnsureSuccessStatusCode();

        using var loginJson = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var loginData = loginJson.RootElement.GetProperty("data");
        var accessToken = loginData.GetProperty("accessToken").GetString();
        var refreshToken = loginData.GetProperty("refreshToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken });
        refreshResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createRoleResponse = await client.PostAsJsonAsync("/api/permission/roles", new
        {
            RoleCode = "auditor",
            RoleName = "审计员"
        });
        if (!createRoleResponse.IsSuccessStatusCode)
        {
            var err = await createRoleResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"create role failed: {(int)createRoleResponse.StatusCode} {err}");
        }

        var grantResponse = await client.PostAsJsonAsync("/api/permission/roles/auditor/grant", new
        {
            Permissions = new[] { "audit.read", "audit.export" }
        });
        grantResponse.EnsureSuccessStatusCode();

        var usersResponse = await client.GetAsync("/api/identity/users");
        usersResponse.EnsureSuccessStatusCode();
        using var usersJson = JsonDocument.Parse(await usersResponse.Content.ReadAsStringAsync());
        var users = usersJson.RootElement.GetProperty("data");
        var adminUserId = users.EnumerateArray().First().GetProperty("userId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(adminUserId));

        var createUserResponse = await client.PostAsJsonAsync("/api/identity/users", new
        {
            Username = "auditor01",
            DisplayName = "审计用户01",
            Password = "Audit@12345"
        });
        createUserResponse.EnsureSuccessStatusCode();
        using var createUserJson = JsonDocument.Parse(await createUserResponse.Content.ReadAsStringAsync());
        var newUserId = createUserJson.RootElement.GetProperty("data").GetProperty("userId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(newUserId));

        var keepEnabledResponse = await client.PostAsJsonAsync($"/api/identity/users/{newUserId}/enabled", new { Enabled = true });
        keepEnabledResponse.EnsureSuccessStatusCode();

        var assignResponse = await client.PostAsJsonAsync($"/api/identity/users/{newUserId}/roles/assign", new
        {
            Roles = new[] { "auditor" }
        });
        assignResponse.EnsureSuccessStatusCode();

        var auditListResponse = await client.GetAsync("/api/audit/events?httpMethod=GET&requestPath=/api/identity/users&limit=10");
        auditListResponse.EnsureSuccessStatusCode();
        using var auditListJson = JsonDocument.Parse(await auditListResponse.Content.ReadAsStringAsync());
        var auditData = auditListJson.RootElement.GetProperty("data");
        var auditItems = auditData.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(auditItems);
        Assert.True(auditData.GetProperty("total").GetInt32() >= auditItems.Count);

        var auditPagedResponse = await client.GetAsync("/api/audit/events?httpMethod=GET&sort=level_asc,occurredAt_desc&page=1&pageSize=1");
        auditPagedResponse.EnsureSuccessStatusCode();
        using var auditPagedJson = JsonDocument.Parse(await auditPagedResponse.Content.ReadAsStringAsync());
        var pagedData = auditPagedJson.RootElement.GetProperty("data");
        var pagedItems = pagedData.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(pagedItems);
        Assert.Equal(1, pagedData.GetProperty("page").GetInt32());
        Assert.Equal(1, pagedData.GetProperty("pageSize").GetInt32());

        var auditExportResponse = await client.GetAsync("/api/audit/events/export?httpMethod=GET&requestPath=/api/identity/users&limit=10&fields=occurredAt,eventCode,actor,statusCode");
        auditExportResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", auditExportResponse.Content.Headers.ContentType?.MediaType);
        var csvContent = await auditExportResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"occurredAt\",\"eventCode\",\"actor\",\"statusCode\"", csvContent);

        var createJobResponse = await client.PostAsJsonAsync("/api/audit/exports", new
        {
            Filter = new
            {
                HttpMethod = "GET",
                RequestPath = "/api/identity/users",
                Limit = 10
            },
            Fields = new[] { "occurredAt", "eventCode", "actor" },
            CallbackUrl = "https://callback.test.local/audit-export"
        });
        createJobResponse.EnsureSuccessStatusCode();
        using var createJobJson = JsonDocument.Parse(await createJobResponse.Content.ReadAsStringAsync());
        var jobData = createJobJson.RootElement.GetProperty("data");
        var jobId = jobData.GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        var statusResponse = await client.GetAsync($"/api/audit/exports/{jobId}");
        statusResponse.EnsureSuccessStatusCode();

        var jobsResponse = await client.GetAsync("/api/audit/exports?page=1&pageSize=10");
        jobsResponse.EnsureSuccessStatusCode();
        using var jobsJson = JsonDocument.Parse(await jobsResponse.Content.ReadAsStringAsync());
        var jobsData = jobsJson.RootElement.GetProperty("data");
        Assert.True(jobsData.GetProperty("total").GetInt32() >= 1);

        HttpResponseMessage? downloadResponse = null;
        for (var i = 0; i < 5; i++)
        {
            downloadResponse = await client.GetAsync($"/api/audit/exports/{jobId}/download");
            if (downloadResponse.IsSuccessStatusCode)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(downloadResponse);
        downloadResponse!.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", downloadResponse.Content.Headers.ContentType?.MediaType);

        CallbackRecord? callback = null;
        for (var i = 0; i < 10; i++)
        {
            if (callbackRecorder.TryDequeue(out callback))
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(callback);
        Assert.Equal("https://callback.test.local/audit-export", callback!.Url);
        Assert.Equal("POST", callback.Method);
        Assert.Equal("audit-export.completed", callback.Headers["X-UniCore-Event"]);
        Assert.True(callback.Headers.TryGetValue("X-UniCore-Timestamp", out var callbackTimestamp));
        Assert.False(string.IsNullOrWhiteSpace(callbackTimestamp));
        Assert.True(callback.Headers.TryGetValue("X-UniCore-Nonce", out var callbackNonce));
        Assert.False(string.IsNullOrWhiteSpace(callbackNonce));
        if (callback.Headers.TryGetValue("X-UniCore-Signature", out var signature))
        {
            Assert.False(string.IsNullOrWhiteSpace(signature));
            var expected = ComputeSignature("integration-test-signing-key", callbackTimestamp!, callbackNonce!, callback.Body);
            Assert.Equal(expected, signature);
        }
        using var callbackJson = JsonDocument.Parse(callback.Body);
        Assert.Equal(jobId, callbackJson.RootElement.GetProperty("jobId").GetString());
        Assert.Equal("Completed", callbackJson.RootElement.GetProperty("status").GetString());
        var callbackDownloadUrl = callbackJson.RootElement.GetProperty("downloadUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(callbackDownloadUrl));
        Assert.Contains($"/api/audit/exports/{jobId}/download", callbackDownloadUrl, StringComparison.Ordinal);

        client.DefaultRequestHeaders.Authorization = null;
        var auditorLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "auditor01",
            Password = "Audit@12345"
        });
        auditorLoginResponse.EnsureSuccessStatusCode();
        using var auditorLoginJson = JsonDocument.Parse(await auditorLoginResponse.Content.ReadAsStringAsync());
        var auditorToken = auditorLoginJson.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(auditorToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auditorToken);
        var forbiddenGet = await client.GetAsync($"/api/audit/exports/{jobId}");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenGet.StatusCode);
        var forbiddenDownload = await client.GetAsync($"/api/audit/exports/{jobId}/download");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenDownload.StatusCode);
    }

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "admin",
            Password = "UniCore@123"
        });
        loginResponse.EnsureSuccessStatusCode();

        using var loginJson = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginJson.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        return accessToken!;
    }

    private static async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition, int timeoutMs, int stepMs)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < TimeSpan.FromMilliseconds(timeoutMs))
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(stepMs);
        }

        return false;
    }

    private static string ComputeSignature(string signingKey, string timestamp, string nonce, string rawBody)
    {
        var originalText = $"{timestamp}.{nonce}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(originalText));
        return Convert.ToHexString(hash);
    }
}

public sealed record CallbackRecord(string Url, string Method, string Body, IReadOnlyDictionary<string, string> Headers);

public sealed class CallbackRecorder
{
    private readonly ConcurrentQueue<CallbackRecord> records = new();

    public void Add(CallbackRecord record) => records.Enqueue(record);

    public bool TryDequeue(out CallbackRecord? record)
    {
        var ok = records.TryDequeue(out var value);
        record = value;
        return ok;
    }
}

public sealed class CallbackHttpClientFactory(
    CallbackRecorder recorder,
    HttpStatusCode responseStatusCode = HttpStatusCode.OK) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new CallbackCaptureHandler(recorder, responseStatusCode));
}

public sealed class CallbackCaptureHandler(
    CallbackRecorder recorder,
    HttpStatusCode responseStatusCode) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(
            x => x.Key,
            x => string.Join(",", x.Value),
            StringComparer.OrdinalIgnoreCase);
        recorder.Add(new CallbackRecord(
            request.RequestUri?.ToString() ?? string.Empty,
            request.Method.Method,
            body,
            headers));

        return new HttpResponseMessage(responseStatusCode);
    }
}
