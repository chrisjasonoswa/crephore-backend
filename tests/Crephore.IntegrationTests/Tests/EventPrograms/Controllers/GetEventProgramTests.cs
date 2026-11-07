using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventPrograms.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventPrograms.Controllers;

public class GetEventProgramTests(DatabaseFixture fixture)
    : EventProgramsBaseTestClass(fixture)
{
    [Fact]
    public async Task Get_ShouldReturnOkAndTheProgram()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        await EventProgramItemFactory.CreateAsync(@event.Id, "Ceremony");

        var response = await client.GetAsync(ApiRoutes.EventPrograms.FullPath(@event.Id));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventProgramDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Title = "Ceremony", Date = "2027-06-12", StartTime = "15:00" });
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenUserHasNoAccessToTheEvent()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var response = await client.GetAsync(ApiRoutes.EventPrograms.FullPath(@event.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(ApiRoutes.EventPrograms.FullPath(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
