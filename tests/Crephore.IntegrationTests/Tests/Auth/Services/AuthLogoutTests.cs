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
    public async Task ShouldRevokeOnlyCurrentSession_WhenUserAndSessionAreValid()
    {
        var user = await AppUserFactory.CreateAsync();
        var sessionOne = await AppUserSessionFactory.CreateAsync(user);
        var sessionTwo = await AppUserSessionFactory.CreateAsync(user);

        await AuthService.LogoutAsync(user.Id, sessionOne.SessionId);

        var sessionOneInDb = await Context.AppUserSessions.AsNoTracking().FirstAsync(s => s.Id == sessionOne.Id);
        var sessionTwoInDb = await Context.AppUserSessions.AsNoTracking().FirstAsync(s => s.Id == sessionTwo.Id);

        sessionOneInDb.RevokedAt.Should().NotBeNull();
        sessionTwoInDb.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldNotRevokeOtherUsersSessions_WhenSessionIdBelongsToAnotherUser()
    {
        var user = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var otherSession = await AppUserSessionFactory.CreateAsync(otherUser);

        await AuthService.LogoutAsync(user.Id, otherSession.SessionId);

        var otherSessionInDb = await Context.AppUserSessions.AsNoTracking().FirstAsync(s => s.Id == otherSession.Id);
        otherSessionInDb.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        var act = async () => await AuthService.LogoutAsync(int.MaxValue, Guid.NewGuid().ToString());

        var exception = await act.Should().ThrowAsync<NotFoundException>();
        exception.Which.Message.Should().Be("User not found.");
    }
}
