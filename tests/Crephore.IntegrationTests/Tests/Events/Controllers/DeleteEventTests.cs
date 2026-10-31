using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Events.Controllers;

public class DeleteEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    [Fact]
    public async Task Delete_ShouldReturnOk_WhenOwnerDeletesTheEvent()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.DeleteAsync($"{ApiRoutes.Events.FullPath}/{@event.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);

        var response = await client.DeleteAsync($"{ApiRoutes.Events.FullPath}/{@event.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        body!.Success.Should().BeFalse();
        body.Message.Should().Be("You do not have access to do this.");
    }

    [Fact]
    public async Task Delete_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync($"{ApiRoutes.Events.FullPath}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
