using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Events.Controllers;

public class ListEventsTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    [Fact]
    public async Task List_ShouldReturnOkAndThePagedEvents_WhenRequestIsAuthenticated()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        await EventFactory.CreateAsync(user.Id, "Birthday");

        var response = await client.GetAsync($"{ApiRoutes.Events.FullPath}?page=1&pageSize=10&sort=name:asc");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ApiPaginationResponseDto<EventDto>>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Single().Name.Should().Be("Birthday");
    }

    [Fact]
    public async Task List_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(ApiRoutes.Events.FullPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ShouldReturnBadRequest_WhenPageHasInvalidFormat()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var __ = client;

        var response = await client.GetAsync($"{ApiRoutes.Events.FullPath}?page=abc");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKey("page");
    }
}
