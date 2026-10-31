using Crephore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.Auth;

public interface ITokenValidationService
{
    Task<bool> IsTokenStateValidAsync(int userId, string sessionId, string tokenSecurityStamp);
}

// Checked on every request (OnTokenValidated). An access token is valid while:
// 1. its session (login) still has an active refresh token: logout revokes them, which signs out
//    only that device; a refresh rotates within the same session, so it stays valid, and
// 2. the user's SecurityStamp is unchanged: password change/reset and "sign out everywhere" rotate it.
public class TokenValidationService(AppDbContext context) : ITokenValidationService
{
    private readonly AppDbContext _context = context;

    public async Task<bool> IsTokenStateValidAsync(int userId, string sessionId, string tokenSecurityStamp)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.AppUserTokens
            .AsNoTracking()
            .AnyAsync(t =>
                t.UserId == userId &&
                t.SessionId == sessionId &&
                t.RevokedAt == null &&
                t.ExpiresAt > now &&
                t.User.SecurityStamp == tokenSecurityStamp);
    }
}
