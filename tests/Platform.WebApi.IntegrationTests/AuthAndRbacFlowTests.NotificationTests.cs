using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Platform.WebApi.IntegrationTests;

public sealed partial class AuthAndRbacFlowTests
{
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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
        var accessToken = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
}
