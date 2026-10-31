using System.Net;
using Crephore.Api.Common.Api;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Controllers;

public class DeleteEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task Delete_ShouldReturnOkAndDeleteTheWebsite()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var response = await client.DeleteAsync(ApiRoutes.EventWebsites.FullPath(@event.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Context.EventWebsites.AsNoTracking().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var response = await client.DeleteAsync(ApiRoutes.EventWebsites.FullPath(@event.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync(ApiRoutes.EventWebsites.FullPath(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
