using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Controllers;

public class UpdateEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    // As the web app sends it
    private static object BuildBody(string primaryColor = "#1E2A44", string? secondaryColor = "#E8A598", string? accentColor = "#C9A227") => BuildBody(
        new { primaryColor, secondaryColor, accentColor, backgroundColor = (string?)null, fontPairingKey = "modern_sans", cornerStyle = "sharp" });

    private static object BuildBody(object theme) => new
    {
        theme,
        sections = new[]
        {
            new
            {
                id = (int?)null,
                type = "hero",
                layout = "fullscreen",
                background = "photo",
                options = new Dictionary<string, string> { ["textPosition"] = "bottom-left" },
                content = new { heading = "Ana & Marco", layoutPhotos = Array.Empty<string>(), cta = new { label = "RSVP", href = "#rsvp" } },
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
        // Left out of the request: the default motion
        result.Data.Theme.Motion.Should().BeEquivalentTo(new { Intro = "shutter", Intensity = "bold" });
        var section = result.Data.Sections.Should().ContainSingle().Subject;
        section.Id.Should().BePositive();
        section.Options.Should().Contain("textPosition", "bottom-left");
        // Left out of the request: Always
        section.Visibility.Should().Be(Crephore.Api.Common.Constants.SectionVisibility.Always);
        section.Content.GetProperty("cta").GetProperty("href").GetString().Should().Be("#rsvp");
    }

    [Fact]
    public async Task Update_ShouldReturnOkAndNullSecondaryAndAccentColors_WhenTheThemeLeavesThemOut()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        // Only the primary colour is required
        var body = BuildBody(new { primaryColor = "#1e2a44", fontPairingKey = "modern_sans", cornerStyle = "sharp" });

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), body, JsonOptions);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var theme = json.RootElement.GetProperty("data").GetProperty("theme");
        theme.GetProperty("primaryColor").GetString().Should().Be("#1E2A44");
        theme.GetProperty("secondaryColor").ValueKind.Should().Be(JsonValueKind.Null);
        theme.GetProperty("accentColor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Update_ShouldReturnOkAndTheSavedMotion_WhenTheThemeHasMotion()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        var body = BuildBody(new
        {
            primaryColor = "#1E2A44", secondaryColor = "#E8A598", fontPairingKey = "modern_sans", cornerStyle = "sharp",
            motion = new { intro = "film-strip", intensity = "subtle" },
        });

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), body, JsonOptions);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var motion = json.RootElement.GetProperty("data").GetProperty("theme").GetProperty("motion");
        motion.EnumerateObject().Select(p => (p.Name, p.Value.GetString())).Should().BeEquivalentTo(new[]
        {
            ("intro", "film-strip"), ("intensity", "subtle"),
        });
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenTheMotionUsesUnknownKeys()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        var body = BuildBody(new
        {
            primaryColor = "#1E2A44", secondaryColor = "#E8A598", fontPairingKey = "modern_sans", cornerStyle = "sharp",
            motion = new { intro = "spin", intensity = "extreme" },
        });

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), body, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!.Keys.Should().BeEquivalentTo("motion.intro", "motion.intensity");
        result.Errors["motion.intro"].Should().Equal("The intro animation is not one Crephore offers.");
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

    [Theory]
    [InlineData("secondaryColor", "The secondary color must be a colour like #1E2A44.")]
    [InlineData("accentColor", "The accent color must be a colour like #1E2A44.")]
    public async Task Update_ShouldReturnBadRequest_WhenAnOptionalColourIsInvalid(string field, string message)
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        var body = field == "secondaryColor" ? BuildBody(secondaryColor: "#E8A59") : BuildBody(accentColor: "gold");

        var response = await client.PutAsJsonAsync(ApiRoutes.EventWebsites.FullPath(@event.Id), body, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!.Keys.Should().Equal(field);
        result.Errors[field].Should().Equal(message);
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
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
