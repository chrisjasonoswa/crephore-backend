using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Controllers;

public class RemoveEventFromEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    private static string GroupEventPath(int groupId, int eventId) =>
        $"{ApiRoutes.EventGroups.FullPath}/{groupId}/{ApiRoutes.EventGroups.Events.RelativeRoute}/{eventId}";

    [Fact]
    public async Task RemoveEvent_ShouldReturnOkAndTheGroupWithoutTheEvent()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var group = await EventGroupFactory.CreateAsync(user.Id);
        var @event = await EventFactory.CreateAsync(user.Id, eventGroupId: group.Id);

        var response = await client.DeleteAsync(GroupEventPath(group.Id, @event.Id));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventGroupDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.EventCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveEvent_ShouldReturnNotFound_WhenEventIsNotInTheGroup()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var group = await EventGroupFactory.CreateAsync(user.Id);
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.DeleteAsync(GroupEventPath(group.Id, @event.Id));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result!.Message.Should().Be("Event is not in this group.");
    }

    [Fact]
    public async Task RemoveEvent_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync(GroupEventPath(1, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
