using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Controllers;

public class GetEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task Get_ShouldReturnOkAndTheWebsite()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var response = await client.GetAsync(ApiRoutes.EventWebsites.FullPath(@event.Id));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventWebsiteDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Theme.FontPairingKey.Should().Be("serif_classic");
        result.Data.Sections.Should().ContainSingle().Which.Content.GetProperty("title").GetString().Should().Be("Ana & Marco");
    }

    [Fact]
    public async Task Get_ShouldReturnOkWithNoData_WhenTheEventHasNoWebsiteYet()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.GetAsync(ApiRoutes.EventWebsites.FullPath(@event.Id));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventWebsiteDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(ApiRoutes.EventWebsites.FullPath(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
