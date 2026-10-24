using Crephore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.Auth;

public interface ISessionValidationService
{
    Task<bool> IsTokenStateValidAsync(int userId, string sessionId, string securityStamp);
}

public class SessionValidationService(AppDbContext context) : ISessionValidationService
{
    private readonly AppDbContext _context = context;

    // A token is valid while:
    // 1. its session is not revoked (logout) or expired, and
    // 2. the user's SecurityStamp is unchanged (rotated on password change/reset to sign out everywhere).
    public async Task<bool> IsTokenStateValidAsync(int userId, string sessionId, string securityStamp)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.AppUserSessions
            .AsNoTracking()
            .AnyAsync(s =>
                s.UserId == userId &&
                s.SessionId == sessionId &&
                s.RevokedAt == null &&
                s.ExpiresAt > now &&
                s.User.SecurityStamp == securityStamp);
    }
}
