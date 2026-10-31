using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventWebsites;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class PublishEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldStoreTheThemeAndVisibleSectionsForGuests_WhenOwnerPublishes()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split", "Shown"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Hidden", isVisible: false),
        ]);

        var website = await EventWebsitesService.PublishAsync(@event.Id, owner.Id);

        website.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        website.HasUnpublishedChanges.Should().BeFalse();

        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync();
        var published = EventWebsitesMapper.ReadPublishedContent(saved.PublishedContent)!;
        published.Theme.PrimaryColor.Should().Be("#1E2A44");
        published.Sections.Should().ContainSingle().Which.Content.GetProperty("title").GetString().Should().Be("Shown");
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

        website.Sections.Single(s => s.IsVisible).Content = """{ "title": "Changed", "layoutPhotos": [] }""";
        await Context.SaveChangesAsync();
        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.HasUnpublishedChanges.Should().BeTrue();
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
