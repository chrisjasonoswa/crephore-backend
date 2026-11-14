using System.Text.Json;
using System.Text.Json.Nodes;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Factories;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class UpdateEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    private static UpdateWebsiteSectionRequestDto BuildSection(int? id = null, string type = "hero", string layout = "split-framed", string heading = "Ana & Marco") => new()
    {
        Id = id,
        Type = type,
        Layout = layout,
        Background = "photo",
        Options = new Dictionary<string, string> { ["photoSide"] = "left" },
        Content = JsonSerializer.SerializeToElement(new { heading, layoutPhotos = Array.Empty<object>() }),
        ContentVersion = 1,
        IsVisible = true,
    };

    private static string UrlOf(Crephore.Api.Data.Entities.Media media) =>
        $"{CustomWebApplicationFactory.StoragePublicBaseUrl}/{media.StorageKey}";

    private static UpdateEventWebsiteRequestDto BuildRequest(params UpdateWebsiteSectionRequestDto[] sections) => new()
    {
        Theme = new WebsiteThemeDto
        {
            PrimaryColor = "#5e7a63",
            SecondaryColor = "#e9dfc8",
            AccentColor = "#B5835a",
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
            BuildRequest(BuildSection(heading: "First"), BuildSection(type: "countdown", layout: "boxes", heading: "Second")));

        website.Sections.Select(s => s.Content.GetProperty("heading").GetString()).Should().Equal("First", "Second");
        website.Theme.PrimaryColor.Should().Be("#5E7A63");
        website.Theme.SecondaryColor.Should().Be("#E9DFC8");
        website.Theme.AccentColor.Should().Be("#B5835A");

        var saved = await Context.EventWebsites.AsNoTracking().Include(w => w.Sections).SingleAsync(w => w.EventId == @event.Id);
        saved.FontPairingKey.Should().Be("romantic");
        saved.BackgroundColor.Should().Be("#FFFCF4");
        new[] { saved.SecondaryColor, saved.AccentColor }.Should().Equal("#E9DFC8", "#B5835A");
        saved.Sections.OrderBy(s => s.SortOrder).Select(s => s.Layout).Should().Equal("split-framed", "boxes");
        saved.Sections.First(s => s.Type == "hero").Options.Should().Contain("photoSide", "left");
    }

    [Fact]
    public async Task ShouldUpdateKeptSectionsAddNewOnesAndDeleteTheRest_WhenTheWebsiteIsSavedAgain()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var existing = await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split-framed", "Kept"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Removed"),
        ]);
        var kept = existing.Sections.Single(s => s.Type == "hero");

        // A new section first, then the kept one with a new layout
        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildSection(type: "countdown", layout: "rings", heading: "New"), BuildSection(id: kept.Id, layout: "centre-stage", heading: "Kept")));

        website.Id.Should().Be(existing.Id);
        website.Sections.Select(s => s.Content.GetProperty("heading").GetString()).Should().Equal("New", "Kept");
        website.Sections[1].Id.Should().Be(kept.Id);

        var sections = await Context.WebsiteSections.AsNoTracking().Where(s => s.EventWebsiteId == existing.Id).OrderBy(s => s.SortOrder).ToListAsync();
        sections.Select(s => s.Layout).Should().Equal("rings", "centre-stage");
        sections[1].Id.Should().Be(kept.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldSaveNoSecondaryOrAccentColor_WhenTheyAreLeftOutOrBlank(string? color)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);
        var request = BuildRequest(BuildSection());
        request.Theme.SecondaryColor = color;
        request.Theme.AccentColor = color;

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        website.Theme.SecondaryColor.Should().BeNull();
        website.Theme.AccentColor.Should().BeNull();
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync(w => w.EventId == @event.Id);
        saved.SecondaryColor.Should().BeNull();
        saved.AccentColor.Should().BeNull();
        var read = (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.Theme;
        read.SecondaryColor.Should().BeNull();
        read.AccentColor.Should().BeNull();
    }

    [Theory]
    [InlineData("light")]
    [InlineData("photo")]
    [InlineData("primary")]
    [InlineData("secondary")]
    public async Task ShouldSaveTheSectionsBackground_WhenItIsAKnownBackground(string background)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var section = BuildSection();
        section.Background = background;

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(section));

        website.Sections.Single().Background.Should().Be(background);
        (await Context.WebsiteSections.AsNoTracking().SingleAsync()).Background.Should().Be(background);
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

    [Theory]
    [InlineData("envelope")]
    [InlineData("ticket-tear")]
    [InlineData("stats")]
    [InlineData("wreath-seal")]
    [InlineData("letter")]
    [InlineData("bunting")]
    [InlineData("split-flap")]
    [InlineData("sampaguita")]
    [InlineData("shutter")]
    [InlineData("counter-scroll")]
    public async Task ShouldSaveASceneIntro_ForEachKindOfEvent(string intro)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);
        var request = BuildRequest(BuildSection());
        request.Theme.Motion = new WebsiteMotionDto { Intro = intro, Intensity = "normal" };

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        website.Theme.Motion.Intro.Should().Be(intro);
    }

    [Fact]
    public async Task ShouldSaveTheMotion_WhenTheThemeHasOne()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);
        var request = BuildRequest(BuildSection());
        request.Theme.Motion = new WebsiteMotionDto { Intro = "card-flip", Intensity = "bold" };
        var expected = new { Intro = "card-flip", Intensity = "bold" };

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        website.Theme.Motion.Should().BeEquivalentTo(expected);
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync(w => w.EventId == @event.Id);
        new[] { saved.MotionIntro, saved.MotionIntensity }.Should().Equal("card-flip", "bold");
        (await EventWebsitesService.GetAsync(@event.Id, owner.Id))!.Theme.Motion.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ShouldSaveTheDefaultMotion_WhenTheThemeLeavesItOut()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id, motion: new WebsiteMotionDto { Intro = "orbit", Intensity = "subtle" });

        // A theme without motion has the defaults
        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection()));

        website.Theme.Motion.Should().BeEquivalentTo(new { Intro = "shutter", Intensity = "bold" });
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync(w => w.EventId == @event.Id);
        new[] { saved.MotionIntro, saved.MotionIntensity }.Should().Equal("shutter", "bold");
    }

    [Fact]
    public async Task ShouldSaveTheHeadingSizes_WhenTheThemeHasThem()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(BuildSection());
        request.Theme.HeroHeadingSize = "extra-large";
        request.Theme.HeadingSize = "small";

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        new[] { website.Theme.HeroHeadingSize, website.Theme.HeadingSize }.Should().Equal("extra-large", "small");
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync(w => w.EventId == @event.Id);
        new[] { saved.HeroHeadingSize, saved.HeadingSize }.Should().Equal("extra-large", "small");
    }

    [Fact]
    public async Task ShouldSaveMediumHeadingSizes_WhenTheThemeLeavesThemOut()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection()));

        new[] { website.Theme.HeroHeadingSize, website.Theme.HeadingSize }.Should().Equal("medium", "medium");
    }

    [Fact]
    public async Task ShouldSaveTheEyebrowStyle_WhenTheThemeHasIt()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(BuildSection());
        request.Theme.EyebrowStyle = "boxed";

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        website.Theme.EyebrowStyle.Should().Be("boxed");
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync(w => w.EventId == @event.Id);
        saved.EyebrowStyle.Should().Be("boxed");
    }

    [Fact]
    public async Task ShouldSavePlainEyebrows_WhenTheThemeLeavesTheStyleOut()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection()));

        website.Theme.EyebrowStyle.Should().Be("plain");
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
        var request = BuildRequest(BuildSection());
        request.Theme.FontPairingKey = "comic_sans";
        request.Theme.CornerStyle = "wavy";
        request.Theme.HeroHeadingSize = "huge";
        request.Theme.HeadingSize = "";
        request.Theme.EyebrowStyle = "underlined";

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKeys("fontPairingKey", "cornerStyle", "heroHeadingSize", "headingSize", "eyebrowStyle");
    }

    [Theory]
    [InlineData("intro", "motion.intro", "The intro animation is not one Crephore offers.")]
    [InlineData("removed intro", "motion.intro", "The intro animation is not one Crephore offers.")]
    [InlineData("intensity", "motion.intensity", "The motion intensity must be subtle, normal or bold.")]
    public async Task ShouldThrowValidationException_WhenTheMotionUsesAnUnknownKey(string key, string field, string message)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(BuildSection());
        switch (key)
        {
            // A key of a dropped scroll animation, a case variant, ...
            case "intro": request.Theme.Motion.Intro = "slide-up"; break;
            // One of the earlier general intros, replaced by the intros for any event
            case "removed intro": request.Theme.Motion.Intro = "fade"; break;
            case "intensity": request.Theme.Motion.Intensity = "extreme"; break;
        }

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, request);

        var exception = (await act.Should().ThrowAsync<AppValidationException>()).Which;
        exception.Errors!.Keys.Should().Equal(field);
        exception.Errors[field].Should().Equal(message);
        exception.Message.Should().Be(message);
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenASectionHasAnUnknownTypeLayoutOrBackground()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var unknownType = BuildSection(type: "gallery");
        var unknownLayout = BuildSection(layout: "boxes");
        var unknownBackground = BuildSection(type: "countdown", layout: "boxes");
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
    public async Task ShouldThrowValidationException_WhenASectionUsesTheRemovedAccentBackground()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        // Renamed to "secondary"; the accent colour is not a section background
        var section = BuildSection();
        section.Background = "accent";

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(section));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should()
            .Equal("Section 1 has an unknown background.");
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenASectionUsesTheRemovedVenueType()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        // Removed: venue details show in the program sections
        var venue = BuildSection(type: "venue", layout: "split-map");

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection(), venue));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should()
            .Equal("Section 2 has an unknown section type.");
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenContentIsNotAnObjectOrTooLarge()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var notAnObject = BuildSection();
        notAnObject.Content = JsonSerializer.SerializeToElement("Ana & Marco");
        var tooLarge = BuildSection(type: "countdown", layout: "boxes");
        tooLarge.Content = JsonSerializer.SerializeToElement(new { heading = new string('a', Crephore.Api.Features.EventWebsites.EventWebsitesService.ContentMaxBytes) });

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(notAnObject, tooLarge));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should().Equal(
            "Section 1 has no content.",
            "Section 2 has too much content.");
    }

    [Fact]
    public async Task ShouldStoreThePhotosAsTheirMediaIdsOnly_AndReturnThemWithTheirUrls()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var background = await MediaFactory.CreatePhotoAsync(@event.Id);
        var layout = await MediaFactory.CreatePhotoAsync(@event.Id);
        var chapter = await MediaFactory.CreatePhotoAsync(@event.Id);
        var section = BuildSection(type: "about", layout: "split");
        // The URLs the web app sends back are dropped; a gap in the layout photos stays
        section.Content = JsonSerializer.SerializeToElement(new
        {
            backgroundPhoto = new { mediaId = background.Id, url = "https://elsewhere.example/a.jpg" },
            layoutPhotos = new object?[] { null, new { mediaId = layout.Id, url = "x" } },
            timeline = new[] { new { heading = "We met", photo = new { mediaId = chapter.Id } } },
            cta = new { href = "https://example.com/photo.jpg" },
        });

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection(), section));

        var saved = await Context.WebsiteSections.AsNoTracking().SingleAsync(s => s.Type == "about");
        JsonNode.DeepEquals(JsonNode.Parse(saved.Content), JsonNode.Parse($$"""
            {
              "backgroundPhoto": { "mediaId": {{background.Id}} },
              "layoutPhotos": [null, { "mediaId": {{layout.Id}} }],
              "timeline": [{ "heading": "We met", "photo": { "mediaId": {{chapter.Id}} } }],
              "cta": { "href": "https://example.com/photo.jpg" }
            }
            """)).Should().BeTrue(saved.Content);

        var content = website.Sections[1].Content;
        content.GetProperty("backgroundPhoto").GetProperty("url").GetString().Should().Be(UrlOf(background));
        content.GetProperty("layoutPhotos")[1].GetProperty("url").GetString().Should().Be(UrlOf(layout));
        content.GetProperty("timeline")[0].GetProperty("photo").GetProperty("url").GetString().Should().Be(UrlOf(chapter));
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenAPhotoIsNotOneOfTheEventsMediaOrHasAnInvalidId()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var otherEvent = await EventFactory.CreateAsync(owner.Id);
        var otherPhoto = await MediaFactory.CreatePhotoAsync(otherEvent.Id);
        var otherEventsPhoto = BuildSection();
        otherEventsPhoto.Content = JsonSerializer.SerializeToElement(new { backgroundPhoto = new { mediaId = otherPhoto.Id } });
        var unknownPhoto = BuildSection(type: "countdown", layout: "boxes");
        unknownPhoto.Content = JsonSerializer.SerializeToElement(new { layoutPhotos = new[] { new { mediaId = otherPhoto.Id + 1000 } } });
        var invalidId = BuildSection(type: "message", layout: "card");
        invalidId.Content = JsonSerializer.SerializeToElement(new { backgroundPhoto = new { mediaId = "12" } });

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(otherEventsPhoto, unknownPhoto, invalidId));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should().Equal(
            "Section 1 has a photo that is no longer available. Upload it again.",
            "Section 2 has a photo that is no longer available. Upload it again.",
            "Section 3 has a photo that isn't valid.");
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenThereIsNoHeroSection()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var empty = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest());
        var noHero = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildSection(type: "countdown", layout: "boxes")));

        foreach (var act in new[] { empty, noHero })
        {
            var exception = (await act.Should().ThrowAsync<AppValidationException>()).Which;
            exception.Message.Should().Be("Add a cover section: every website needs one.");
            exception.Errors!["sections"].Should().Equal("Add a cover section: every website needs one.");
        }
        // The saved website keeps its hero
        (await Context.WebsiteSections.CountAsync(s => s.Type == "hero")).Should().Be(1);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenThereIsMoreThanOneHeroSection()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventWebsitesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildSection(heading: "First"), BuildSection(type: "countdown", layout: "boxes"), BuildSection(heading: "Second")));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["sections"].Should()
            .Equal("A website can only have one cover section.");
        (await Context.EventWebsites.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldSaveAHiddenHero_WhenItIsTheOnlyHero()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var hidden = BuildSection();
        hidden.IsVisible = false;

        var website = await EventWebsitesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(hidden, BuildSection(type: "countdown", layout: "boxes")));

        website.Sections.Should().HaveCount(2);
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
