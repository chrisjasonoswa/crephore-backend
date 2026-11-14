using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventVenues.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventVenues.Controllers;

public class UpdateEventVenuesTests(DatabaseFixture fixture)
    : EventVenuesBaseTestClass(fixture)
{
    // As the web app sends it
    private static object BuildBody(string name = "Manila Cathedral") => new
    {
        venues = new[]
        {
            new
            {
                id = (int?)null,
                name,
                address = (string?)"Cabildo St, Intramuros, Manila",
                googleMapsUrl = (string?)"https://maps.app.goo.gl/AbCdEf123",
                appleMapsUrl = (string?)null,
                latitude = (decimal?)14.591500m,
                longitude = (decimal?)120.973600m,
                photo = (object?)null,
                notes = (string?)"Enter through the side gate.",
            },
        },
    };

    [Fact]
    public async Task Update_ShouldReturnOkAndTheSavedVenues()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventVenues.FullPath(@event.Id), BuildBody(), JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<EventVenueDto>>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var venue = result!.Data.Should().ContainSingle().Subject;
        venue.Id.Should().BePositive();
        venue.Should().BeEquivalentTo(new
        {
            Name = "Manila Cathedral",
            GoogleMapsUrl = "https://maps.app.goo.gl/AbCdEf123",
            Latitude = (decimal?)14.5915m,
            Longitude = (decimal?)120.9736m,
            Photo = (object?)null,
            Notes = "Enter through the side gate.",
        });
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequestWithErrorsKeyedByVenue_WhenAVenueIsInvalid()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventVenues.FullPath(@event.Id), BuildBody(name: ""), JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!.Keys.Should().BeEquivalentTo("venues[0].name");
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenTheVenuesAreMissing()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventVenues.FullPath(@event.Id), new { venues = (object?)null }, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKey("venues");
    }

    [Fact]
    public async Task Update_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventVenues.FullPath(@event.Id), BuildBody(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync(ApiRoutes.EventVenues.FullPath(1), BuildBody(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
