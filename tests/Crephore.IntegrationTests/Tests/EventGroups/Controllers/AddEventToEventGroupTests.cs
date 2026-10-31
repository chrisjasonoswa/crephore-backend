using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Controllers;

public class AddEventToEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    private static string GroupEventPath(int groupId, int eventId) =>
        $"{ApiRoutes.EventGroups.FullPath}/{groupId}/{ApiRoutes.EventGroups.Events.RelativeRoute}/{eventId}";

    [Fact]
    public async Task AddEvent_ShouldReturnOkAndTheGroupWithTheEvent()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var group = await EventGroupFactory.CreateAsync(user.Id);
        var @event = await EventFactory.CreateAsync(user.Id, "Birthday");

        var response = await client.PostAsync(GroupEventPath(group.Id, @event.Id), content: null);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventGroupDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Events.Single().Name.Should().Be("Birthday");
    }

    [Fact]
    public async Task AddEvent_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(GroupEventPath(1, 1), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
