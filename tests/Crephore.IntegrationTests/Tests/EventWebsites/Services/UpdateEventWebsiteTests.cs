using System.Text.Json;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class UpdateEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    private static UpdateWebsiteSectionRequestDto BuildSection(int? id = null, string type = "hero", string layout = "split", string title = "Ana & Marco") => new()
    {
        Id = id,
        Type = type,
        Layout = layout,
        Background = "photo",
        Options = new Dictionary<string, string> { ["photoSide"] = "left" },
        Content = JsonSerializer.SerializeToElement(new { title, backgroundPhoto = "http://localhost/uploads/a.jpg", layoutPhotos = Array.Empty<string>() }),
        ContentVersion = 1,
        IsVisible = true,
    };

    private static UpdateEventWebsiteRequestDto BuildRequest(params UpdateWebsiteSectionRequestDto[] sections) => new()
    {
        Theme = new WebsiteThemeDto
        {
            PrimaryColor = "#5e7a63",
            AccentColor = "#E9DFC8",
            BackgroundColor = "#FFFCF4",
            FontPairingKey = "romantic",
            CornerStyle = "round",
        },
        Sections = [.. sections],
    };

    [Fact]
    public async Task ShouldCreateTheWebsite_WhenItIsSavedForTheFirstTime()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildSection(title: "First"), BuildSection(type: "countdown", layout: "boxes", title: "Second")));

        website.Sections.Select(s => s.Content.GetProperty("title").GetString()).Should().Equal("First", "Second");
        website.Theme.PrimaryColor.Should().Be("#5E7A63");

        var saved = await Context.EventWebsites.AsNoTracking().Include(w => w.Sections).SingleAsync(w => w.EventId == @event.Id);
        saved.FontPairingKey.Should().Be("romantic");
        saved.BackgroundColor.Should().Be("#FFFCF4");
        saved.Sections.OrderBy(s => s.SortOrder).Select(s => s.Layout).Should().Equal("split", "boxes");
        saved.Sections.First(s => s.Type == "hero").Options.Should().Contain("photoSide", "left");
    }

    [Fact]
    public async Task ShouldUpdateKeptSectionsAddNewOnesAndDeleteTheRest_WhenTheWebsiteIsSavedAgain()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var existing = await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split", "Kept"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Removed"),
        ]);
        var kept = existing.Sections.Single(s => s.Type == "hero");

        // A new section first, then the kept one with a new layout
        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildSection(type: "countdown", layout: "rings", title: "New"), BuildSection(id: kept.Id, layout: "arch", title: "Kept")));

        website.Id.Should().Be(existing.Id);
        website.Sections.Select(s => s.Content.GetProperty("title").GetString()).Should().Equal("New", "Kept");
        website.Sections[1].Id.Should().Be(kept.Id);

        var sections = await Context.WebsiteSections.AsNoTracking().Where(s => s.EventWebsiteId == existing.Id).OrderBy(s => s.SortOrder).ToListAsync();
        sections.Select(s => s.Layout).Should().Equal("rings", "arch");
        sections[1].Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task ShouldSaveEachSectionsVisibility_AlwaysWhenLeftOut()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var beforeEvent = BuildSection(type: "countdown", layout: "boxes");
        beforeEvent.Visibility = SectionVisibility.BeforeEvent;

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection(), beforeEvent));

        website.Sections.Select(s => s.Visibility).Should().Equal(SectionVisibility.Always, SectionVisibility.BeforeEvent);
        (await Context.WebsiteSections.AsNoTracking().OrderBy(s => s.SortOrder).Select(s => s.Visibility).ToListAsync())
            .Should().Equal(SectionVisibility.Always, SectionVisibility.BeforeEvent);
    }

    [Fact]
    public async Task ShouldRemoveEverySection_WhenNoSectionsAreSent()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest());

        website.Sections.Should().BeEmpty();
        (await Context.WebsiteSections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldCreateOneWebsite_WhenItIsFirstSavedTwiceAtOnce()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(BuildSection());

        var results = await RunConcurrentlyAsync<IEventWebsitesService>(2, service => service.UpdateAsync(@event.Id, owner.Id, request));

        // Either the second save updates the first one's website, or (when both create one) the index rejects it
        // with a field error, never a 500
        results.Should().Contain(exception => exception == null);
        foreach (var exception in results.Where(exception => exception != null))
            exception.Should().BeOfType<AppValidationException>().Which.Errors!.Should().ContainKey("website");
        (await Context.EventWebsites.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenTheThemeUsesUnknownKeys()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest();
        request.Theme.FontPairingKey = "comic_sans";
        request.Theme.CornerStyle = "wavy";

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKeys("fontPairingKey", "cornerStyle");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenASectionHasAnUnknownTypeLayoutOrBackground()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var unknownType = BuildSection(type: "gallery");
        var unknownLayout = BuildSection(layout: "boxes");
        var unknownBackground = BuildSection();
        unknownBackground.Background = "dark";

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(unknownType, unknownLayout, unknownBackground));

        var exception = (await act.Should().ThrowAsync<AppValidationException>()).Which;
        exception.Message.Should().Be("Section 1 has an unknown section type.");
        exception.Errors!["sections"].Should().Equal(
            "Section 1 has an unknown section type.",
            "Section 2 has an unknown layout.",
            "Section 3 has an unknown background.");
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenContentIsNotAnObjectOrTooLarge()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var notAnObject = BuildSection();
        notAnObject.Content = JsonSerializer.SerializeToElement("Ana & Marco");
        var tooLarge = BuildSection();
        tooLarge.Content = JsonSerializer.SerializeToElement(new { title = new string('a', Crephore.Api.Features.EventWebsites.EventWebsitesService.ContentMaxBytes) });

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(notAnObject, tooLarge));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should().Equal(
            "Section 1 has no content.",
            "Section 2 has too much content.");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenASectionIdIsNotOnThisWebsite()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var otherEvent = await EventFactory.CreateAsync(owner.Id);
        var otherWebsite = await EventWebsiteFactory.CreateAsync(otherEvent.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildSection(id: otherWebsite.Sections.Single().Id)));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should()
            .ContainSingle().Which.Should().Contain("no longer on this website");
        (await Context.WebsiteSections.CountAsync(s => s.EventWebsiteId == otherWebsite.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, member.Id, BuildRequest(BuildSection()));

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, otherUser.Id, BuildRequest(BuildSection()));

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
