using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventPrograms.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventPrograms.Controllers;

public class UpdateEventProgramTests(DatabaseFixture fixture)
    : EventProgramsBaseTestClass(fixture)
{
    // As the web app sends it
    private static object BuildBody(string title = "Ceremony", string date = "2027-06-12", int? venueId = null) => new
    {
        items = new[]
        {
            new { id = (int?)null, date, startTime = (string?)"15:00", endTime = (string?)null, title, description = (string?)null, venueId, speakers = (string?)null },
        },
    };

    [Fact]
    public async Task Update_ShouldReturnOkAndTheSavedProgram()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral");

        var response = await client.PutAsJsonAsync(ApiRoutes.EventPrograms.FullPath(@event.Id), BuildBody(venueId: venues[0].Id), JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventProgramDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = result!.Data!.Items.Should().ContainSingle().Subject;
        item.Id.Should().BePositive();
        item.Should().BeEquivalentTo(new { Date = "2027-06-12", StartTime = "15:00", Title = "Ceremony", VenueId = (int?)venues[0].Id });
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequestWithErrorsKeyedByItem_WhenAnItemIsInvalid()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventPrograms.FullPath(@event.Id), BuildBody(title: "", date: "soon"), JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!.Keys.Should().BeEquivalentTo("items[0].date", "items[0].title");
    }

    [Fact]
    public async Task Update_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventPrograms.FullPath(@event.Id), BuildBody(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync(ApiRoutes.EventPrograms.FullPath(1), BuildBody(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
