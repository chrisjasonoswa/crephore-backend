using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Auth.Services;

public class AuthLoginTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnTokenAndCreateSession_WhenCredentialsAreValid()
    {
        var user = await AppUserFactory.CreateAsync();

        var result = await AuthService.LoginAsync(user.Email!, AppUserFactory.GetValidPassword);

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.SessionId.Should().NotBeNullOrWhiteSpace();
        result.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        result.User.Id.Should().Be(user.Id);
        result.User.Email.Should().Be(user.Email!);

        // Token is bound to the user and the new session
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.First(c => c.Type == CustomClaimTypes.SessionId).Value.Should().Be(result.SessionId);

        // Session persisted
        var sessionInDb = await Context.AppUserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == user.Id && s.SessionId == result.SessionId);
        sessionInDb.Should().NotBeNull();
        sessionInDb!.RevokedAt.Should().BeNull();
        sessionInDb.ExpiresAt.Should().BeCloseTo(result.AccessTokenExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ShouldReturnToken_WhenEmailCaseAndWhitespaceDiffer()
    {
        var user = await AppUserFactory.CreateAsync();

        var result = await AuthService.LoginAsync($" {user.Email!.ToUpperInvariant()} ", AppUserFactory.GetValidPassword);

        result.User.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task ShouldAddSuperAdminRoleClaim_WhenUserIsSuperAdmin()
    {
        var user = await AppUserFactory.CreateAsync(isSuperAdmin: true);

        var result = await AuthService.LoginAsync(user.Email!, AppUserFactory.GetValidPassword);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == DefaultRoles.SuperAdmin);
    }

    [Fact]
    public async Task ShouldNotAddSuperAdminRoleClaim_WhenUserIsNotSuperAdmin()
    {
        var user = await AppUserFactory.CreateAsync();

        var result = await AuthService.LoginAsync(user.Email!, AppUserFactory.GetValidPassword);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Claims.Should().NotContain(c => c.Type == "role" || c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task ShouldThrowUnauthorizedException_WhenPasswordIsWrong()
    {
        var user = await AppUserFactory.CreateAsync();

        var act = async () => await AuthService.LoginAsync(user.Email!, "WrongPassword123");

        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.Message.Should().Be("Invalid credentials");

        (await Context.AppUserSessions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldThrowLockedOutException_WhenMaxFailedAttemptsReached()
    {
        var user = await AppUserFactory.CreateAsync();

        // Default lockout threshold is 5 failed attempts
        for (var i = 0; i < 5; i++)
        {
            var failedAttempt = async () => await AuthService.LoginAsync(user.Email!, "WrongPassword123");
            await failedAttempt.Should().ThrowAsync<UnauthorizedException>();
        }

        // Even the correct password is rejected while locked out
        var act = async () => await AuthService.LoginAsync(user.Email!, AppUserFactory.GetValidPassword);

        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.Message.Should().Be("Too many failed login attempts. Please try again later.");
        (await UserManager.IsLockedOutAsync(user)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldResetFailedAttemptCount_WhenLoginSucceeds()
    {
        var user = await AppUserFactory.CreateAsync();

        var failedAttempt = async () => await AuthService.LoginAsync(user.Email!, "WrongPassword123");
        await failedAttempt.Should().ThrowAsync<UnauthorizedException>();

        await AuthService.LoginAsync(user.Email!, AppUserFactory.GetValidPassword);

        (await UserManager.GetAccessFailedCountAsync(user)).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowUnauthorizedException_WhenEmailDoesNotExist()
    {
        var act = async () => await AuthService.LoginAsync("missing@test.com", AppUserFactory.GetValidPassword);

        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.Message.Should().Be("Invalid credentials");
    }
}
