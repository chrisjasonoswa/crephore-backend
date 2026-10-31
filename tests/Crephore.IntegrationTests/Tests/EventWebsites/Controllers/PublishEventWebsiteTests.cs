using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Controllers;

public class PublishEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task Publish_ShouldReturnOkAndThePublishedWebsite()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var response = await client.PostAsync(ApiRoutes.EventWebsites.Publish.FullPath(@event.Id), null);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventWebsiteDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.PublishedAt.Should().NotBeNull();
        result.Data.HasUnpublishedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task Publish_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var response = await client.PostAsync(ApiRoutes.EventWebsites.Publish.FullPath(@event.Id), null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Publish_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(ApiRoutes.EventWebsites.Publish.FullPath(1), null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
