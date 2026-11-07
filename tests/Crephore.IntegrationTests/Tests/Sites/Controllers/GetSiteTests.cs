using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.Sites.Dtos;
using Crephore.IntegrationTests.Factories.Entities;
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
    public async Task Get_ShouldReturnTheThemesThreeColours()
    {
        using var client = CreateClient();
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        var response = await client.GetAsync($"{ApiRoutes.Sites.FullPath}/ana-and-marco");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var theme = json.RootElement.GetProperty("data").GetProperty("theme");
        new[] { "primaryColor", "secondaryColor", "accentColor" }.Select(name => theme.GetProperty(name).GetString())
            .Should().Equal("#1E2A44", "#E8A598", "#C9A227");
    }

    [Fact]
    public async Task Get_ShouldReturnTheThemesMotion()
    {
        using var client = CreateClient();
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true,
            motion: new() { Intro = "pin-drop", Intensity = "bold" });
        // Published while motion also had scroll animations: only the intro and intensity reach guests
        website.PublishedContent = EventWebsiteFactory.WithScrollMotion(website.PublishedContent!);
        await Context.SaveChangesAsync();

        var response = await client.GetAsync($"{ApiRoutes.Sites.FullPath}/ana-and-marco");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var motion = json.RootElement.GetProperty("data").GetProperty("theme").GetProperty("motion");
        motion.EnumerateObject().Select(p => (p.Name, p.Value.GetString())).Should().BeEquivalentTo(new[]
        {
            ("intro", "pin-drop"), ("intensity", "bold"),
        });
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
