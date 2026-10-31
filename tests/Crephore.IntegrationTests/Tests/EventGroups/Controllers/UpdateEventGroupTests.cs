using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Controllers;

public class UpdateEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task Update_ShouldReturnOkAndTheRenamedGroup()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var group = await EventGroupFactory.CreateAsync(user.Id, "Family");

        var response = await client.PutAsJsonAsync($"{ApiRoutes.EventGroups.FullPath}/{group.Id}", new UpdateEventGroupRequestDto { Name = "Relatives" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventGroupDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Name.Should().Be("Relatives");
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenNameIsEmpty()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var group = await EventGroupFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.EventGroups.FullPath}/{group.Id}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync($"{ApiRoutes.EventGroups.FullPath}/1", new UpdateEventGroupRequestDto { Name = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
