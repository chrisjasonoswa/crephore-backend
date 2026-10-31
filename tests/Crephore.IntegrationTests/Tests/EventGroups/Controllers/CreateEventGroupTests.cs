using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventGroups.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Controllers;

public class CreateEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task Create_ShouldReturnOkAndTheGroup()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.PostAsJsonAsync(ApiRoutes.EventGroups.FullPath, new CreateEventGroupRequestDto { Name = "Family" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventGroupDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Name.Should().Be("Family");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenNameIsEmpty()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.PostAsJsonAsync(ApiRoutes.EventGroups.FullPath, new CreateEventGroupRequestDto { Name = " " });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKey("name");
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.EventGroups.FullPath, new CreateEventGroupRequestDto { Name = "Family" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
