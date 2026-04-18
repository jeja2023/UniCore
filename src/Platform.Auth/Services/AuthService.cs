using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Platform.Auth.Services;

public sealed class AuthService(
    AppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IAppCache cache,
    IOptions<LoginSecurityOptions> loginSecurityOptions)
{
    private readonly PasswordHasher<UserEntity> _passwordHasher = new();
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly LoginSecurityOptions _loginSecurity = loginSecurityOptions.Value;

    public async Task<LoginResult> LoginAsync(string username, string password, string tenantId = "default", CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId.Trim();
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new UnauthorizedAccessException("用户名或密码错误");
        }

        var lockKey = $"auth:lock:{normalizedTenantId}:{normalizedUsername}";
        var locked = await cache.GetAsync<string>(lockKey, cancellationToken);
        if (!string.IsNullOrEmpty(locked))
        {
            throw new UnauthorizedAccessException($"账号已锁定，请稍后再试（约 {_loginSecurity.LockMinutes} 分钟）。");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == normalizedTenantId && x.Username == username, cancellationToken);
        if (user is null || !user.Enabled)
        {
            await RegisterLoginFailureAsync(normalizedTenantId, normalizedUsername, cancellationToken);
            throw new UnauthorizedAccessException("用户名或密码错误");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RegisterLoginFailureAsync(normalizedTenantId, normalizedUsername, cancellationToken);
            throw new UnauthorizedAccessException("用户名或密码错误");
        }

        await cache.RemoveAsync($"auth:fail:{normalizedTenantId}:{normalizedUsername}", cancellationToken);

        var roles = await (from ur in dbContext.UserRoles
                           join r in dbContext.Roles on ur.RoleId equals r.RoleId
                           where ur.UserId == user.UserId
                           select r.RoleCode).ToArrayAsync(cancellationToken);

        var accessToken = BuildAccessToken(user, roles);
        var refreshToken = new RefreshTokenEntity
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            Token = $"rt-{Guid.NewGuid():N}",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpireDays),
            Revoked = false
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult(accessToken, refreshToken.Token, _jwtOptions.AccessTokenExpireMinutes * 60, user.Username, user.TenantId, roles);
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.Token == refreshToken, cancellationToken);
        if (tokenEntity is null || tokenEntity.Revoked || tokenEntity.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedAccessException("RefreshToken 无效");
        }

        var user = await dbContext.Users.SingleAsync(x => x.UserId == tokenEntity.UserId, cancellationToken);
        if (!user.Enabled)
        {
            tokenEntity.Revoked = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("用户已被禁用");
        }

        var roles = await (from ur in dbContext.UserRoles
                           join r in dbContext.Roles on ur.RoleId equals r.RoleId
                           where ur.UserId == user.UserId
                           select r.RoleCode).ToArrayAsync(cancellationToken);

        tokenEntity.Revoked = true;
        var newRefreshToken = new RefreshTokenEntity
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            Token = $"rt-{Guid.NewGuid():N}",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpireDays),
            Revoked = false
        };
        dbContext.RefreshTokens.Add(newRefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult(BuildAccessToken(user, roles), newRefreshToken.Token, _jwtOptions.AccessTokenExpireMinutes * 60, user.Username, user.TenantId, roles);
    }

    public async Task<LoginResult> LoginByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleAsync(x => x.UserId == userId && x.Enabled, cancellationToken);
        var roles = await (from ur in dbContext.UserRoles
                           join r in dbContext.Roles on ur.RoleId equals r.RoleId
                           where ur.UserId == user.UserId
                           select r.RoleCode).ToArrayAsync(cancellationToken);
        var refreshToken = new RefreshTokenEntity
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            Token = $"rt-{Guid.NewGuid():N}",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpireDays),
            Revoked = false
        };
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new LoginResult(BuildAccessToken(user, roles), refreshToken.Token, _jwtOptions.AccessTokenExpireMinutes * 60, user.Username, user.TenantId, roles);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenEntity = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.Token == refreshToken, cancellationToken);
        if (tokenEntity is null)
        {
            return;
        }

        tokenEntity.Revoked = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RegisterLoginFailureAsync(string tenantId, string username, CancellationToken cancellationToken)
    {
        var failKey = $"auth:fail:{tenantId}:{username}";
        var current = await cache.GetAsync<int?>(failKey, cancellationToken) ?? 0;
        var next = current + 1;
        await cache.SetAsync(failKey, next, TimeSpan.FromMinutes(_loginSecurity.FailWindowMinutes), cancellationToken);

        if (next >= _loginSecurity.MaxFailAttempts)
        {
            await cache.SetAsync($"auth:lock:{tenantId}:{username}", "1", TimeSpan.FromMinutes(_loginSecurity.LockMinutes), cancellationToken);
        }
    }

    private string BuildAccessToken(UserEntity user, IReadOnlyCollection<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("tenant_id", user.TenantId)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpireMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

public sealed record LoginResult(string AccessToken, string RefreshToken, int ExpiresInSeconds, string Username, string TenantId, IReadOnlyCollection<string> Roles);

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "UniCore";
    public string Audience { get; set; } = "UniCore.Admin";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenExpireMinutes { get; set; } = 60;
    public int RefreshTokenExpireDays { get; set; } = 7;
}

public sealed class LoginSecurityOptions
{
    public const string Section = "LoginSecurity";

    public int MaxFailAttempts { get; set; } = 5;

    public int FailWindowMinutes { get; set; } = 10;

    public int LockMinutes { get; set; } = 10;
}
