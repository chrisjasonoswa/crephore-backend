using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Constants;
using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Events.Controllers;

public class CreateEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    // Wall-clock time at the venue (Asia/Manila, UTC+8), as the web app sends it
    private static readonly DateTime Start = new(2027, 6, 12, 15, 0, 0);

    private static CreateEventRequestDto BuildValidRequest() => new()
    {
        Name = "Ana & Marco",
        Type = EventType.Wedding,
        VenueName = "Taal Vista",
        VenueAddress = "Tagaytay, Philippines",
        StartsAt = Start,
        EndsAt = Start.AddHours(8),
        Timezone = "Asia/Manila",
        Subdomain = $"ana-and-marco-{Guid.NewGuid().ToString("N")[..6]}",
        Timeline = ["Ceremony"],
    };

    [Fact]
    public async Task Create_ShouldReturnOkAndTheEvent_WithTypeAsString()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.PostAsJsonAsync(ApiRoutes.Events.FullPath, BuildValidRequest(), JsonOptions);
        var body = await response.Content.ReadAsStringAsync();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"type\":\"Wedding\"");
        // Not yet filled (no Google Places integration), sent and returned as null
        body.Should().Contain("\"venueLatitude\":null");
        result!.Data!.Timeline.Should().Equal("Ceremony");
        // Wall-clock time in, UTC + the event's timezone out
        result.Data.Timezone.Should().Be("Asia/Manila");
        result.Data.StartsAt.Should().Be(new DateTimeOffset(2027, 6, 12, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenATimeIsSentWithAnOffset()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.PostAsJsonAsync(ApiRoutes.Events.FullPath, new
        {
            name = "Ana & Marco",
            type = "Wedding",
            venueName = "Taal Vista",
            venueAddress = "Tagaytay, Philippines",
            startsAt = "2027-06-12T15:00:00+08:00",
            endsAt = "2027-06-12T23:00",
            timezone = "Asia/Manila",
            subdomain = $"ana-and-marco-{Guid.NewGuid().ToString("N")[..6]}",
        });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKey("startsAt").And.NotContainKey("endsAt");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenRequiredFieldsAreMissing()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.PostAsJsonAsync(ApiRoutes.Events.FullPath, new { });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Message.Should().Be("Some fields are invalid.");
        result.Errors.Should().ContainKeys("name", "type", "venueName", "venueAddress", "startsAt", "endsAt", "timezone", "subdomain");
        result.Errors!["subdomain"].Should().Contain("The website address is required.");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenTypeIsUnknown()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;
        var request = BuildValidRequest();

        var response = await client.PostAsJsonAsync(ApiRoutes.Events.FullPath, new
        {
            request.Name,
            Type = "Barbecue",
            request.VenueName,
            request.VenueAddress,
            request.StartsAt,
            request.EndsAt,
            request.Subdomain,
        }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Events.FullPath, BuildValidRequest(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
