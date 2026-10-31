using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Controllers;

public class ListEventGroupsTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task List_ShouldReturnOkAndThePagedGroups_WhenRequestIsAuthenticated()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        await EventGroupFactory.CreateAsync(user.Id, "Family");

        var response = await client.GetAsync(ApiRoutes.EventGroups.FullPath);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ApiPaginationResponseDto<EventGroupDto>>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Items.Single().Name.Should().Be("Family");
    }

    [Fact]
    public async Task List_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(ApiRoutes.EventGroups.FullPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
