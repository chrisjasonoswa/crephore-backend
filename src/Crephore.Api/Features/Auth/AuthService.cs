using Crephore.Api.Common.Authentication;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.AppUsers;
using Crephore.Api.Features.AppUsers.Dtos;
using Crephore.Api.Features.Auth.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.Auth;

public interface IAuthService
{
    Task<AppUserDto> SignupAsync(SignupRequestDto request);
    Task<LoginResponseDto> LoginAsync(string email, string password);
    Task<RefreshResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(int userId, string sessionId);
    Task<AppUserDto> GetCurrentUserAsync(int userId);
}


public class AuthService(
    UserManager<AppUser> _userManager,
    AppDbContext _context,
    ITokenProvider _tokenProvider,
    IHttpContextAccessor _httpContextAccessor,
    ILogger<AuthService> _logger
) : IAuthService
{
    public async Task<AppUserDto> SignupAsync(SignupRequestDto request)
    {
        string email = request.Email.Trim();

        // Lookup is case-insensitive (NormalizedEmail)
        if (await _userManager.FindByEmailAsync(email) is not null)
            throw new AppValidationException(
                "Email already exists",
                new Dictionary<string, List<string>>
                {
                    ["email"] = ["Email already exists."]
                });

        var appUser = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            // Identity requires a UserName; users log in by email, so generate an opaque one.
            UserName = $"user_{Guid.NewGuid():N}",
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            // Super admins are never created through public signup.
            IsSuperAdmin = false,
        };

        var createdUser = await _userManager.CreateAsync(appUser, request.Password);

        if (!createdUser.Succeeded)
        {
            throw new AppValidationException(
                createdUser.Errors
                    .GroupBy(e => MapIdentityErrorToField(e))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.Description).ToList()
                    )
            );
        }

        return appUser.ToAppUserDto();
    }

    public async Task<LoginResponseDto> LoginAsync(string email, string password)
    {
        AppUser appUser = await _userManager.FindByEmailAsync(email.Trim())
            ?? throw new UnauthorizedException("Invalid credentials");

        if (await _userManager.IsLockedOutAsync(appUser))
            throw new UnauthorizedException("Too many failed login attempts. Please try again later.");

        // CheckPasswordAsync also upgrades the hash when the hasher's algorithm/iterations change
        if (!await _userManager.CheckPasswordAsync(appUser, password))
        {
            // Counts toward lockout (Lockout options in ProgramServiceExtensions.AddIdentity)
            await _userManager.AccessFailedAsync(appUser);
            throw new UnauthorizedException("Invalid credentials");
        }

        await _userManager.ResetAccessFailedCountAsync(appUser);


        // http data
        var http = _httpContextAccessor.HttpContext;
        var userAgent = http?.Request.Headers.UserAgent.ToString();
        var createdByIp = http?.Connection.RemoteIpAddress?.ToString();

        // Create refresh token and save in db
        string refreshToken = _tokenProvider.CreateRefreshToken();
        string sessionId = Guid.NewGuid().ToString();

        // Create access token
        var accessTokenResult = _tokenProvider.CreateToken(appUser, sessionId);

        var sessionExpiresAt = DateTimeOffset.UtcNow.Add(TokenProvider.SessionMaxLifetime);
        var refreshTokenExpiresAt = TokenProvider.RefreshTokenExpiresAt(sessionExpiresAt);

        _context.AppUserTokens.Add(new AppUserToken
        {
            UserId = appUser.Id,
            TokenHash = _tokenProvider.HashToken(refreshToken),
            SecurityStamp = appUser.SecurityStamp ?? string.Empty,
            ExpiresAt = refreshTokenExpiresAt,
            SessionExpiresAt = sessionExpiresAt,
            SessionId = sessionId,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : Truncate(userAgent, 512),
            CreatedByIp = createdByIp,
        });
        await _context.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessTokenResult.AccessToken,
            RefreshToken = refreshToken,
            SessionId = sessionId,
            AccessTokenExpiresAt = accessTokenResult.AccessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            User = appUser.ToAppUserDto(),
        };
    }

    // Rotates the refresh token: the old one is revoked and points to the new one.
    // A replaced token sent again within RefreshTokenReuseGracePeriod (e.g. by another tab) gets new tokens
    // for the same login; sent later, it is treated as stolen and ends the whole login.
    // Every rejection answers the same "Your session has expired." (the reason is only logged).
    public async Task<RefreshResponseDto> RefreshAsync(string refreshToken)
    {
        string refreshTokenHash = _tokenProvider.HashToken(refreshToken);

        AppUserToken? currentToken = await _context.AppUserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == refreshTokenHash);

        if (currentToken is null)
        {
            _logger.LogWarning("Refresh rejected: token not found.");
            throw SessionExpired("not_found");
        }

        var now = DateTimeOffset.UtcNow;
        var http = _httpContextAccessor.HttpContext;
        var ip = http?.Connection.RemoteIpAddress?.ToString();
        AppUser appUser = currentToken.User;

        if (currentToken.ReplacedByTokenHash is not null)
        {
            var isWithinGracePeriod = currentToken.RevokedAt >= now - TokenProvider.RefreshTokenReuseGracePeriod;
            var isSessionActive = await _context.AppUserTokens.AnyAsync(t =>
                t.UserId == currentToken.UserId &&
                t.SessionId == currentToken.SessionId &&
                t.RevokedAt == null &&
                t.ExpiresAt > now);

            if (!isWithinGracePeriod || !isSessionActive)
            {
                // Reused after its replacement was issued: whoever holds it, end the login
                _logger.LogWarning(
                    "Refresh rejected: rotated token reused; ending the session. UserId={UserId}, SessionId={SessionId}",
                    currentToken.UserId,
                    currentToken.SessionId
                );
                await RevokeSessionAsync(currentToken.UserId, currentToken.SessionId, ip);
                throw SessionExpired("token_reused");
            }
        }
        else if (!currentToken.IsActive)
        {
            _logger.LogWarning(
                "Refresh rejected: expired or revoked token. UserId={UserId}, SessionId={SessionId}",
                currentToken.UserId,
                currentToken.SessionId
            );
            throw SessionExpired("inactive");
        }

        // Password change/reset and "sign out everywhere" rotate the stamp
        if (!string.Equals(currentToken.SecurityStamp, appUser.SecurityStamp, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Refresh rejected: security stamp mismatch. UserId={UserId}, SessionId={SessionId}",
                currentToken.UserId,
                currentToken.SessionId
            );

            currentToken.RevokedAt ??= now;
            currentToken.RevokedByIp ??= ip;
            await _context.SaveChangesAsync();

            throw SessionExpired("security_stamp_mismatch");
        }

        var userAgent = http?.Request.Headers.UserAgent.ToString();

        var newAccessTokenResult = _tokenProvider.CreateToken(appUser, currentToken.SessionId);
        string newRefreshToken = _tokenProvider.CreateRefreshToken();
        string newRefreshTokenHash = _tokenProvider.HashToken(newRefreshToken);
        var refreshTokenExpiresAt = TokenProvider.RefreshTokenExpiresAt(currentToken.SessionExpiresAt);

        // Update old token (a grace-period reuse keeps its first replacement)
        if (currentToken.ReplacedByTokenHash is null)
        {
            currentToken.RevokedAt = now;
            currentToken.RevokedByIp = ip;
            currentToken.ReplacedByTokenHash = newRefreshTokenHash;
        }

        // Create new token in the same session, with the same hard limit
        _context.AppUserTokens.Add(new AppUserToken
        {
            UserId = currentToken.UserId,
            TokenHash = newRefreshTokenHash,
            SecurityStamp = appUser.SecurityStamp ?? string.Empty,
            ExpiresAt = refreshTokenExpiresAt,
            SessionExpiresAt = currentToken.SessionExpiresAt,
            SessionId = currentToken.SessionId,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? currentToken.UserAgent : Truncate(userAgent, 512),
            CreatedByIp = ip,
        });

        await _context.SaveChangesAsync();

        return new RefreshResponseDto
        {
            AccessToken = newAccessTokenResult.AccessToken,
            RefreshToken = newRefreshToken,
            SessionId = currentToken.SessionId,
            AccessTokenExpiresAt = newAccessTokenResult.AccessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
        };
    }

    // Signs out this device only: revokes this session's refresh tokens, which also rejects its access token
    // (TokenValidationService). Other devices stay signed in; use UserManager.UpdateSecurityStampAsync
    // to sign the user out everywhere.
    public async Task LogoutAsync(int userId, string sessionId)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == userId))
            throw new NotFoundException("User not found.");

        var revokedByIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        await RevokeSessionAsync(userId, sessionId, revokedByIp);
    }


    public async Task<AppUserDto> GetCurrentUserAsync(int userId)
    {
        AppUser appUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("User not found.");

        return appUser.ToAppUserDto();
    }


    // Helpers
    private static string MapIdentityErrorToField(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateEmail" => "email",
            "InvalidEmail" => "email",
            "PasswordTooShort" => "password",
            "PasswordRequiresNonAlphanumeric" => "password",
            "PasswordRequiresDigit" => "password",
            "PasswordRequiresUpper" => "password",
            "PasswordRequiresLower" => "password",
            "PasswordRequiresUniqueChars" => "password",
            _ => "general"
        };
    }

    // Revokes every active refresh token of one login, which also rejects its access tokens
    private async Task RevokeSessionAsync(int userId, string sessionId, string? revokedByIp)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionTokens = await _context.AppUserTokens
            .Where(t => t.UserId == userId && t.SessionId == sessionId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in sessionTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = revokedByIp;
        }

        await _context.SaveChangesAsync();
    }

    private static UnauthorizedException SessionExpired(string reason) =>
        new("Your session has expired.", new Dictionary<string, List<string>>
        {
            ["refreshToken"] = [reason],
        });

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
