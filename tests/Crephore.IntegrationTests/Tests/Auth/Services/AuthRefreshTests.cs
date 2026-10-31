using System.IdentityModel.Tokens.Jwt;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Auth.Services;

public class AuthRefreshTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldRotateTheRefreshToken_AndIssueANewAccessTokenForTheSameSession()
    {
        var user = await AppUserFactory.CreateAsync();
        var (oldToken, refreshToken) = await AppUserTokenFactory.CreateAsync(user);

        var result = await AuthService.RefreshAsync(refreshToken);

        result.RefreshToken.Should().NotBe(refreshToken);
        result.SessionId.Should().Be(oldToken.SessionId);
        new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken)
            .Claims.First(c => c.Type == CustomClaimTypes.SessionId).Value.Should().Be(oldToken.SessionId);

        // The old token is revoked and points to its replacement; the new one is active
        var newHash = TokenProvider.HashToken(result.RefreshToken);
        var oldInDb = await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.Id == oldToken.Id);
        oldInDb.RevokedAt.Should().NotBeNull();
        oldInDb.ReplacedByTokenHash.Should().Be(newHash);
        var newInDb = await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.TokenHash == newHash);
        newInDb.IsActive.Should().BeTrue();
        newInDb.SessionId.Should().Be(oldToken.SessionId);
    }

    [Fact]
    public async Task ShouldKeepTheSessionGoing_WhenAReplacedTokenIsReusedWithinTheGracePeriod()
    {
        // e.g. two tabs refreshing at the same time with the same cookie
        var user = await AppUserFactory.CreateAsync();
        var (oldToken, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        var first = await AuthService.RefreshAsync(refreshToken);

        var second = await AuthService.RefreshAsync(refreshToken);

        second.SessionId.Should().Be(oldToken.SessionId);
        second.RefreshToken.Should().NotBe(first.RefreshToken);
        // The old token keeps pointing to its first replacement, and both new tokens work
        (await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.Id == oldToken.Id)).ReplacedByTokenHash
            .Should().Be(TokenProvider.HashToken(first.RefreshToken));
        (await Context.AppUserTokens.AsNoTracking().CountAsync(t => t.SessionId == oldToken.SessionId && t.RevokedAt == null))
            .Should().Be(2);
    }

    [Fact]
    public async Task ShouldEndTheWholeSession_WhenAReplacedTokenIsReusedAfterTheGracePeriod()
    {
        var user = await AppUserFactory.CreateAsync();
        var (oldToken, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        var first = await AuthService.RefreshAsync(refreshToken);
        // Replaced a minute ago
        await Context.AppUserTokens.Where(t => t.Id == oldToken.Id)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        // The service shares this context; drop its cached copy so it reads the back-dated row
        Context.ChangeTracker.Clear();

        var act = async () => await AuthService.RefreshAsync(refreshToken);

        (await act.Should().ThrowAsync<UnauthorizedException>()).Which.Errors!["refreshToken"].Should().Equal("token_reused");
        // Likely stolen: every token of the login is revoked, including the latest one
        (await Context.AppUserTokens.AsNoTracking().AnyAsync(t => t.SessionId == oldToken.SessionId && t.RevokedAt == null))
            .Should().BeFalse();
        var latest = async () => await AuthService.RefreshAsync(first.RefreshToken);
        await latest.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ShouldRejectAReplacedToken_WithinTheGracePeriod_WhenTheSessionWasLoggedOut()
    {
        var user = await AppUserFactory.CreateAsync();
        var (oldToken, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        await AuthService.RefreshAsync(refreshToken);
        await AuthService.LogoutAsync(user.Id, oldToken.SessionId);

        var act = async () => await AuthService.RefreshAsync(refreshToken);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ShouldNeverExtendTheSessionPastItsHardLimit()
    {
        var user = await AppUserFactory.CreateAsync();
        var sessionExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var (_, refreshToken) = await AppUserTokenFactory.CreateAsync(user, sessionExpiresAt: sessionExpiresAt);

        var result = await AuthService.RefreshAsync(refreshToken);

        // Capped at the login's limit, not 7 days from now
        result.RefreshTokenExpiresAt.Should().BeCloseTo(sessionExpiresAt, TimeSpan.FromSeconds(1));
        var newInDb = await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.TokenHash == TokenProvider.HashToken(result.RefreshToken));
        newInDb.SessionExpiresAt.Should().BeCloseTo(sessionExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ShouldThrowUnauthorizedException_WhenRefreshTokenIsUnknown()
    {
        var act = async () => await AuthService.RefreshAsync("not-a-real-token");

        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.Message.Should().Be("Your session has expired.");
        exception.Which.Errors!["refreshToken"].Should().Equal("not_found");
    }

    [Fact]
    public async Task ShouldThrowUnauthorizedException_WhenRefreshTokenIsExpired()
    {
        var user = await AppUserFactory.CreateAsync();
        var (_, refreshToken) = await AppUserTokenFactory.CreateAsync(user, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var act = async () => await AuthService.RefreshAsync(refreshToken);

        (await act.Should().ThrowAsync<UnauthorizedException>()).Which.Errors!["refreshToken"]
            .Should().Equal("inactive");
    }

    [Fact]
    public async Task ShouldThrowUnauthorizedException_WhenRefreshTokenIsRevoked()
    {
        var user = await AppUserFactory.CreateAsync();
        var (_, refreshToken) = await AppUserTokenFactory.CreateAsync(user, revokedAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var act = async () => await AuthService.RefreshAsync(refreshToken);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ShouldRevokeAndReject_WhenTheSecurityStampChanged()
    {
        var user = await AppUserFactory.CreateAsync();
        var (token, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        // e.g. a password change or "sign out everywhere"
        await UserManager.UpdateSecurityStampAsync(user);

        var act = async () => await AuthService.RefreshAsync(refreshToken);

        (await act.Should().ThrowAsync<UnauthorizedException>()).Which.Errors!["refreshToken"]
            .Should().Equal("security_stamp_mismatch");
        (await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.Id == token.Id)).RevokedAt.Should().NotBeNull();
    }
}
