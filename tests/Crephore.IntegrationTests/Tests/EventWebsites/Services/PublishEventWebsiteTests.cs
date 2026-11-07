using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class PublishEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    private static readonly WebsiteMotionDto DefaultMotion = new() { Intro = "shutter", Intensity = "bold" };

    [Fact]
    public async Task ShouldStoreTheThemeAndVisibleSectionsForGuests_WhenOwnerPublishes()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split", "Shown"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Hidden", isVisible: false),
        ], motion: new() { Intro = "card-flip", Intensity = "bold" });

        var website = await EventWebsitesService.PublishAsync(@event.Id, owner.Id);

        website.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        website.HasUnpublishedChanges.Should().BeFalse();

        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync();
        var published = EventWebsitesMapper.ReadPublishedContent(saved.PublishedContent)!;
        new[] { published.Theme.PrimaryColor, published.Theme.SecondaryColor, published.Theme.AccentColor }
            .Should().Equal("#1E2A44", "#E8A598", "#C9A227");
        published.Theme.Motion.Should().BeEquivalentTo(new { Intro = "card-flip", Intensity = "bold" });
        published.Sections.Should().ContainSingle().Which.Content.GetProperty("heading").GetString().Should().Be("Shown");
        // Stored like the API's JSON: enums by name
        saved.PublishedContent.Should().Contain("\"visibility\": \"Always\"");
    }

    [Fact]
    public async Task ShouldReportUnpublishedChanges_OnlyWhenWhatGuestsSeeHasChanged()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var website = await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split", "Shown"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Hidden", isVisible: false),
        ], published: true);

        // Read back from MySQL, which stores JSON with its keys reordered: still the same website
        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeFalse();

        // A hidden section's change doesn't reach guests
        website.Sections.Single(s => !s.IsVisible).Layout = "rings";
        await Context.SaveChangesAsync();
        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeFalse();

        website.Sections.Single(s => s.IsVisible).Content = """{ "heading": "Changed", "layoutPhotos": [] }""";
        await Context.SaveChangesAsync();
        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeTrue();
    }

    [Theory]
    [InlineData("intro")]
    [InlineData("intensity")]
    public async Task ShouldReportUnpublishedChanges_WhenOnlyTheMotionHasChanged(string key)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        switch (key)
        {
            case "intro": website.MotionIntro = "orbit"; break;
            case "intensity": website.MotionIntensity = "subtle"; break;
        }
        await Context.SaveChangesAsync();

        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeTrue();
    }

    [Theory]
    [InlineData("secondary", "#112233")]
    [InlineData("secondary", null)]
    [InlineData("accent", "#112233")]
    [InlineData("accent", null)]
    public async Task ShouldReportUnpublishedChanges_WhenOnlyTheSecondaryOrAccentColorHasChanged(string color, string? value)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        switch (color)
        {
            case "secondary": website.SecondaryColor = value; break;
            case "accent": website.AccentColor = value; break;
        }
        await Context.SaveChangesAsync();

        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldReadTheDefaultMotionAndReportNoChanges_WhenPublishedBeforeMotionExisted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        website.PublishedContent = EventWebsiteFactory.WithoutMotion(website.PublishedContent!);
        await Context.SaveChangesAsync();

        var result = await EventWebsitesService.GetAsync(@event.Id, owner.Id);

        result!.HasUnpublishedChanges.Should().BeFalse();
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync();
        saved.PublishedContent.Should().NotContain("motion");
        EventWebsitesMapper.ReadPublishedContent(saved.PublishedContent)!.Theme.Motion
            .Should().BeEquivalentTo(DefaultMotion);

        // Choosing other motion is a change guests don't see yet
        website.MotionIntro = "card-flip";
        await Context.SaveChangesAsync();
        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldReadTheDefaultMotionAndReportNoChanges_WhenPublishedWithTheOldMotionShape()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        // { transition, animation, intensity }, from before motion had an intro
        website.PublishedContent = EventWebsiteFactory.WithOldMotion(website.PublishedContent!, transition: "orbit", animation: "pop");
        await Context.SaveChangesAsync();

        var result = await EventWebsitesService.GetAsync(@event.Id, owner.Id);

        result!.HasUnpublishedChanges.Should().BeFalse();
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync();
        saved.PublishedContent.Should().Contain("transition");
        EventWebsitesMapper.ReadPublishedContent(saved.PublishedContent)!.Theme.Motion.Should().BeEquivalentTo(DefaultMotion);
    }

    [Fact]
    public async Task ShouldReadTheDefaultMotionAndReportNoChanges_WhenPublishedWithTheScrollAnimations()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        // { intro, section, text, media, intensity }, from before the scroll animations were dropped
        website.PublishedContent = EventWebsiteFactory.WithScrollMotion(website.PublishedContent!);
        await Context.SaveChangesAsync();

        var result = await EventWebsitesService.GetAsync(@event.Id, owner.Id);

        result!.HasUnpublishedChanges.Should().BeFalse();
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync();
        saved.PublishedContent.Should().Contain("parallax");
        EventWebsitesMapper.ReadPublishedContent(saved.PublishedContent)!.Theme.Motion.Should().BeEquivalentTo(DefaultMotion);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenNoSectionIsVisible()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id, [EventWebsiteFactory.BuildSection(isVisible: false)]);

        var act = async () => await EventWebsitesService.PublishAsync(@event.Id, owner.Id);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Message.Should().Be("Add at least one visible section before publishing.");
        (await Context.EventWebsites.AsNoTracking().SingleAsync()).PublishedContent.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenTheWebsiteWasNeverSaved()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventWebsitesService.PublishAsync(@event.Id, owner.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Save your website before publishing it.");
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var act = async () => await EventWebsitesService.PublishAsync(@event.Id, member.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.EventWebsites.AsNoTracking().SingleAsync()).PublishedContent.Should().BeNull();
    }
}
