using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Auth.Services;

public class AuthLogoutTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldRevokeTheSessionsRefreshTokens_WhenUserAndSessionAreValid()
    {
        var user = await AppUserFactory.CreateAsync();
        var (sessionOne, _) = await AppUserTokenFactory.CreateAsync(user);
        var (sessionTwo, _) = await AppUserTokenFactory.CreateAsync(user);

        await AuthService.LogoutAsync(user.Id, sessionOne.SessionId);

        (await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.Id == sessionOne.Id)).RevokedAt.Should().NotBeNull();
        // Other devices stay signed in
        (await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.Id == sessionTwo.Id)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldNotChangeTheSecurityStamp_SoOtherDevicesStaySignedIn()
    {
        var user = await AppUserFactory.CreateAsync();
        var (token, _) = await AppUserTokenFactory.CreateAsync(user);
        var stampBefore = user.SecurityStamp;

        await AuthService.LogoutAsync(user.Id, token.SessionId);

        var userInDb = await Context.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        userInDb.SecurityStamp.Should().Be(stampBefore);
    }

    [Fact]
    public async Task ShouldNotRevokeOtherUsersTokens_WhenSessionIdBelongsToAnotherUser()
    {
        var user = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var (otherToken, _) = await AppUserTokenFactory.CreateAsync(otherUser);

        await AuthService.LogoutAsync(user.Id, otherToken.SessionId);

        (await Context.AppUserTokens.AsNoTracking().FirstAsync(t => t.Id == otherToken.Id)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        var act = async () => await AuthService.LogoutAsync(int.MaxValue, Guid.NewGuid().ToString());

        var exception = await act.Should().ThrowAsync<NotFoundException>();
        exception.Which.Message.Should().Be("User not found.");
    }
}
