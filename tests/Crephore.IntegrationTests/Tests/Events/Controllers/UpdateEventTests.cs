using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Constants;
using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Events.Controllers;

public class UpdateEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    private static readonly DateTime Start = new(2027, 6, 12, 15, 0, 0);

    private static UpdateEventRequestDto BuildRequest(string subdomain) => new()
    {
        Name = "Ana & Marco",
        Type = EventType.Wedding,
        VenueName = "Taal Vista",
        VenueAddress = "Tagaytay, Philippines",
        StartsAt = Start,
        EndsAt = Start.AddHours(8),
        Timezone = "Asia/Manila",
        Subdomain = subdomain,
        Timeline = ["Ceremony", "Reception"],
    };

    [Fact]
    public async Task Update_ShouldReturnOkAndTheEvent()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Events.FullPath}/{@event.Id}", BuildRequest(@event.Subdomain), JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Name.Should().Be("Ana & Marco");
        result.Data.Timeline.Should().Equal("Ceremony", "Reception");
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenRequiredFieldsAreMissing()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Events.FullPath}/{@event.Id}", new { });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKeys("name", "startsAt", "timezone", "subdomain");
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Events.FullPath}/1", BuildRequest("any"), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
