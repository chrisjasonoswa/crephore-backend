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
    Task LogoutAsync(int userId, string sessionId);
    Task<AppUserDto> GetCurrentUserAsync(int userId);
}


public class AuthService(
    UserManager<AppUser> _userManager,
    AppDbContext _context,
    ITokenProvider _tokenProvider,
    IHttpContextAccessor _httpContextAccessor
) : IAuthService
{
    private const string DefaultTimezone = "UTC";

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

        string timezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? DefaultTimezone
            : request.Timezone.Trim();

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _))
            throw new AppValidationException(
                new Dictionary<string, List<string>>
                {
                    ["timezone"] = ["The timezone is not a valid IANA timezone."]
                });

        var appUser = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            // Identity requires a UserName; users log in by email, so generate an opaque one.
            UserName = $"user_{Guid.NewGuid():N}",
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Timezone = timezone,
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

        // Create access token bound to a new session
        string sessionId = Guid.NewGuid().ToString();
        var accessTokenResult = _tokenProvider.CreateToken(appUser, sessionId);

        _context.AppUserSessions.Add(new AppUserSession
        {
            UserId = appUser.Id,
            SessionId = sessionId,
            ExpiresAt = accessTokenResult.AccessTokenExpiresAt,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : Truncate(userAgent, 512),
            CreatedByIp = createdByIp,
        });
        await _context.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessTokenResult.AccessToken,
            SessionId = sessionId,
            AccessTokenExpiresAt = accessTokenResult.AccessTokenExpiresAt,
            User = appUser.ToAppUserDto(),
        };
    }

    // Revokes only the current session. Other devices stay signed in;
    // use UserManager.UpdateSecurityStampAsync to sign the user out everywhere.
    public async Task LogoutAsync(int userId, string sessionId)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == userId))
            throw new NotFoundException("User not found.");

        var http = _httpContextAccessor.HttpContext;
        var revokedByIp = http?.Connection.RemoteIpAddress?.ToString();
        var now = DateTimeOffset.UtcNow;

        var sessions = await _context.AppUserSessions
            .Where(s => s.UserId == userId && s.SessionId == sessionId && s.RevokedAt == null)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.RevokedByIp = revokedByIp;
        }

        await _context.SaveChangesAsync();
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

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
