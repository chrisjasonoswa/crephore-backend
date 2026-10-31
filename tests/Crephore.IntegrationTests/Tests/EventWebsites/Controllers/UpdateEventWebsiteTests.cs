using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Controllers;

public class UpdateEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    // As the web app sends it
    private static object BuildBody(string primaryColor = "#1E2A44") => new
    {
        theme = new { primaryColor, accentColor = "#E8A598", backgroundColor = (string?)null, fontPairingKey = "modern_sans", cornerStyle = "sharp" },
        sections = new[]
        {
            new
            {
                id = (int?)null,
                type = "hero",
                layout = "fullscreen",
                background = "photo",
                options = new Dictionary<string, string> { ["textPosition"] = "bottom-left" },
                content = new { title = "Ana & Marco", layoutPhotos = Array.Empty<string>(), cta = new { label = "RSVP", href = "#rsvp" } },
                contentVersion = 1,
                isVisible = true,
            },
        },
    };

    [Fact]
    public async Task Update_ShouldReturnOkAndTheSavedWebsite()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), BuildBody(), JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<EventWebsiteDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.Theme.CornerStyle.Should().Be("sharp");
        var section = result.Data.Sections.Should().ContainSingle().Subject;
        section.Id.Should().BePositive();
        section.Options.Should().Contain("textPosition", "bottom-left");
        // Left out of the request: Always
        section.Visibility.Should().Be(Crephore.Api.Common.Constants.SectionVisibility.Always);
        section.Content.GetProperty("cta").GetProperty("href").GetString().Should().Be("#rsvp");
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenTheThemeIsMissingOrAColourIsInvalid()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var missing = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), new { sections = Array.Empty<object>() });
        var invalidColour = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), BuildBody("navy"), JsonOptions);
        var result = await invalidColour.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidColour.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!["primaryColor"].Should().Equal("The primary color must be a colour like #1E2A44.");
    }

    [Fact]
    public async Task Update_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), BuildBody(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(1), BuildBody(), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
