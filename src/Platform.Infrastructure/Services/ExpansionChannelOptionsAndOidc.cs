namespace Platform.Infrastructure.Services;

using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net.Http.Headers;

public sealed class EmailChannelOptions
{
    public const string Section = "NotificationChannels:Email";
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@unicore.local";
}

public sealed class SmsChannelOptions
{
    public const string Section = "NotificationChannels:Sms";
    public bool Enabled { get; set; }
    public string ProviderUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string SenderId { get; set; } = "UniCore";
}

public sealed class OidcSsoOptions
{
    public const string Section = "Sso:Oidc";
    public bool Enabled { get; set; }
    public List<OidcProviderConfig> Providers { get; set; } = [];
}

public sealed class OidcProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInfoEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed record OidcUserProfile(string Provider, string Subject, string? Username, string? DisplayName, string? Email);

public sealed class OidcSsoService(IHttpClientFactory httpClientFactory, IOptions<OidcSsoOptions> options)
{
    public async Task<OidcUserProfile> ExchangeCodeAsync(string provider, string code, string redirectUri, string? codeVerifier, CancellationToken cancellationToken = default)
    {
        var config = options.Value.Providers.SingleOrDefault(x => string.Equals(x.Name, provider, StringComparison.OrdinalIgnoreCase));
        if (!options.Value.Enabled || config is null)
        {
            throw new InvalidOperationException($"OIDC Provider 未配置或未启用: {provider}");
        }

        var tokenContent = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        };
        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            tokenContent["code_verifier"] = codeVerifier;
        }

        var client = httpClientFactory.CreateClient(nameof(OidcSsoService));
        using var tokenResp = await client.PostAsync(config.TokenEndpoint, new FormUrlEncodedContent(tokenContent), cancellationToken);
        if (!tokenResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC Token 交换失败: {(int)tokenResp.StatusCode}");
        }

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("OIDC Token 响应缺少 access_token。");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, config.UserInfoEndpoint);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var userInfoResp = await client.SendAsync(req, cancellationToken);
        if (!userInfoResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC UserInfo 获取失败: {(int)userInfoResp.StatusCode}");
        }
        using var userInfoDoc = JsonDocument.Parse(await userInfoResp.Content.ReadAsStringAsync(cancellationToken));
        var subject = userInfoDoc.RootElement.TryGetProperty("sub", out var sub)
            ? sub.GetString()
            : userInfoDoc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("OIDC UserInfo 缺少 sub。");
        }

        var username = userInfoDoc.RootElement.TryGetProperty("preferred_username", out var pu) ? pu.GetString() : null;
        var displayName = userInfoDoc.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        var email = userInfoDoc.RootElement.TryGetProperty("email", out var em) ? em.GetString() : null;
        return new OidcUserProfile(config.Name, subject!, username, displayName, email);
    }
}
