using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Platform.WebApi.IntegrationTests;

public sealed partial class AuthAndRbacFlowTests
{
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
}
