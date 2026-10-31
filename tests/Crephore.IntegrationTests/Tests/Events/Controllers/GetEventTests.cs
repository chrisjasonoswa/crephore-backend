using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Events.Controllers;

public class GetEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    [Fact]
    public async Task Get_ShouldReturnOkAndTheEvent_WhenOwnerRequestsIt()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id, "Birthday", timeline: ["Opening"]);

        var response = await client.GetAsync($"{ApiRoutes.Events.FullPath}/{@event.Id}");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Name.Should().Be("Birthday");
        result.Data.Timeline.Should().Equal("Opening");
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.GetAsync($"{ApiRoutes.Events.FullPath}/{@event.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"{ApiRoutes.Events.FullPath}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
