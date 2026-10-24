using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Data.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Crephore.Api.Common.Authentication;

public interface ITokenProvider
{
    TokenSessionResult CreateToken(AppUser user, string sessionId);
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
}
