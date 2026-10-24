using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

public class AppUserSessionFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<AppUserSession> CreateAsync(
        AppUser user,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null,
        string? sessionId = null
    )
    {
        var session = new AppUserSession
        {
            UserId = user.Id,
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(1),
            RevokedAt = revokedAt,
            CreatedByIp = "127.0.0.1",
            UserAgent = "integration-test-agent",
        };

        _context.AppUserSessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }
}
