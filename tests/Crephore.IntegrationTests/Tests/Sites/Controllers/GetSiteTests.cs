using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.Sites.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Sites.Controllers;

public class GetSiteTests(DatabaseFixture fixture)
    : SitesBaseTestClass(fixture)
{
    [Fact]
    public async Task Get_ShouldReturnOkAndThePublishedWebsite_WithoutSigningIn()
    {
        using var client = CreateClient();
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, name: "Ana & Marco", subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        var response = await client.GetAsync($"{ApiRoutes.Sites.FullPath}/ana-and-marco");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SiteDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.EventName.Should().Be("Ana & Marco");
        result.Data.Sections.Should().ContainSingle().Which.Type.Should().Be("hero");
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenTheWebsiteIsNotPublished()
    {
        using var client = CreateClient();
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var response = await client.GetAsync($"{ApiRoutes.Sites.FullPath}/ana-and-marco");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
