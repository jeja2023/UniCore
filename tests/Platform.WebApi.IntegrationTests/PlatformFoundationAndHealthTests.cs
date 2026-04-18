using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Platform.WebApi.IntegrationTests;

public sealed partial class AuthAndRbacFlowTests
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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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

        var expiredDefaultTenantJobId = Guid.NewGuid();
        var expiredOtherTenantJobId = Guid.NewGuid();
        var freshOtherTenantJobId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditExportJobs.AddRange(
                new AuditExportJobEntity
                {
                    JobId = expiredDefaultTenantJobId,
                    TenantId = "default",
                    CreatedBy = "cleanup-test-user",
                    Status = "Completed",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    Completed = true,
                    CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    CsvContent = "\"header\"\n\"value\""
                },
                new AuditExportJobEntity
                {
                    JobId = expiredOtherTenantJobId,
                    TenantId = "tenant-ops",
                    CreatedBy = "cleanup-test-user",
                    Status = "Completed",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-12),
                    Completed = true,
                    CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-11),
                    CsvContent = "\"header\"\n\"value\""
                },
                new AuditExportJobEntity
                {
                    JobId = freshOtherTenantJobId,
                    TenantId = "tenant-ops",
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
            var expiredDefaultExists = await db.AuditExportJobs.AnyAsync(x => x.JobId == expiredDefaultTenantJobId);
            var expiredOtherTenantExists = await db.AuditExportJobs.AnyAsync(x => x.JobId == expiredOtherTenantJobId);
            var freshOtherTenantExists = await db.AuditExportJobs.AnyAsync(x => x.JobId == freshOtherTenantJobId);
            return !expiredDefaultExists && !expiredOtherTenantExists && freshOtherTenantExists;
        }, timeoutMs: 5000, stepMs: 100);

        Assert.True(cleaned);
    }
}
