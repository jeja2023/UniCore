using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.AuditLog.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Platform.WebApi.IntegrationTests;

public sealed partial class AuthAndRbacFlowTests
{
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
}
