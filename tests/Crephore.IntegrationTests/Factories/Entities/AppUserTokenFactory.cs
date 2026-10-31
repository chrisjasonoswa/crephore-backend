using Crephore.Api.Common.Authentication;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

// A refresh token row; returns the raw token too (only its hash is stored)
public class AppUserTokenFactory(AppDbContext context, ITokenProvider tokenProvider)
{
    private readonly AppDbContext _context = context;
    private readonly ITokenProvider _tokenProvider = tokenProvider;

    public async Task<(AppUserToken Token, string RefreshToken)> CreateAsync(
        AppUser user,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null,
        string? sessionId = null,
        string? securityStamp = null,
        DateTimeOffset? sessionExpiresAt = null
    )
    {
        var sessionLimit = sessionExpiresAt ?? DateTimeOffset.UtcNow.Add(TokenProvider.SessionMaxLifetime);
        var refreshToken = _tokenProvider.CreateRefreshToken();
        var token = new AppUserToken
        {
            UserId = user.Id,
            TokenHash = _tokenProvider.HashToken(refreshToken),
            SecurityStamp = securityStamp ?? user.SecurityStamp ?? string.Empty,
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            ExpiresAt = expiresAt ?? TokenProvider.RefreshTokenExpiresAt(sessionLimit),
            SessionExpiresAt = sessionLimit,
            RevokedAt = revokedAt,
            CreatedByIp = "127.0.0.1",
            UserAgent = "integration-test-agent",
        };

        _context.AppUserTokens.Add(token);
        await _context.SaveChangesAsync();

        return (token, refreshToken);
    }
}
