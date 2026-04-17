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
using Xunit;

namespace Platform.WebApi.IntegrationTests;

public sealed partial class AuthAndRbacFlowTests
{

    [Fact]
    public async Task TenantSettingsEndpoints_ShouldUpsertAndReadSettings()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
    public async Task CurrentUserContextEndpoint_ShouldReturnActualPermissionsAndMenus()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseInMemoryDatabase", "true");
            });
        using var client = factory.CreateClient();

        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/me/context");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"username\":\"admin\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"permissions\":", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user.read", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"menus\":", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/identity/users", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/modules", body, StringComparison.OrdinalIgnoreCase);
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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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

