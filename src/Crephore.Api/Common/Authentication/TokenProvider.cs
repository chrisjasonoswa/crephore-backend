using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Data.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Crephore.Api.Common.Authentication;

public interface ITokenProvider
{
    TokenSessionResult CreateToken(AppUser user, string sessionId);
    string CreateRefreshToken();
    string HashToken(string token);
}

public sealed record TokenSessionResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt)
{
    public static implicit operator string(TokenSessionResult result) => result.AccessToken;
}


public class TokenProvider(
    IConfiguration config
) : ITokenProvider
{
    private readonly IConfiguration _config = config;
    // How long a refresh token lasts without being used; each refresh issues a new one
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // A login never lasts longer than this, however active it is; then the user signs in again
    public static readonly TimeSpan SessionMaxLifetime = TimeSpan.FromDays(30);

    // A replaced refresh token still works this long, for requests (e.g. other tabs) that sent it
    // before the new one arrived. Reused later, it ends the whole login (likely stolen).
    public static readonly TimeSpan RefreshTokenReuseGracePeriod = TimeSpan.FromSeconds(10);

    // A new refresh token's expiry: RefreshTokenLifetime from now, but never past the login's limit
    public static DateTimeOffset RefreshTokenExpiresAt(DateTimeOffset sessionExpiresAt)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime);
        return expiresAt < sessionExpiresAt ? expiresAt : sessionExpiresAt;
    }

    private readonly SymmetricSecurityKey _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:SigningKey"]!));

    public TokenSessionResult CreateToken(AppUser user, string sessionId)
    {
        var now = DateTime.UtcNow;
        var accessTokenExpiresAtUtc = now.AddMinutes(_config.GetValue<int>("JWT:ExpirationInMinutes"));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(CustomClaimTypes.SecurityStamp, user.SecurityStamp ?? string.Empty),
            new(CustomClaimTypes.SessionId, sessionId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        if (user.IsSuperAdmin)
            claims.Add(new Claim(ClaimTypes.Role, DefaultRoles.SuperAdmin));

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _config["JWT:Issuer"],
            Audience = _config["JWT:Audience"],
            NotBefore = now,
            Expires = accessTokenExpiresAtUtc,
            SigningCredentials = creds,
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new TokenSessionResult(tokenHandler.WriteToken(token), new DateTimeOffset(accessTokenExpiresAtUtc));
    }

    public string CreateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    // Only the hash is stored, so a leaked database cannot be used to refresh
    public string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}
