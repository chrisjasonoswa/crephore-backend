using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventVenues.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventVenues.Controllers;

public class GetEventVenuesTests(DatabaseFixture fixture)
    : EventVenuesBaseTestClass(fixture)
{
    [Fact]
    public async Task Get_ShouldReturnOkAndTheVenuesInOrder()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");

        var response = await client.GetAsync(ApiRoutes.EventVenues.FullPath(@event.Id));
        var body = await response.Content.ReadAsStringAsync();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<EventVenueDto>>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Select(v => v.Name).Should().Equal("Manila Cathedral", "Shangri-La");
        // Optional fields are sent as null when unset
        body.Should().Contain("\"googleMapsUrl\":null").And.Contain("\"latitude\":null");
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenUserHasNoAccessToTheEvent()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var response = await client.GetAsync(ApiRoutes.EventVenues.FullPath(@event.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(ApiRoutes.EventVenues.FullPath(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
