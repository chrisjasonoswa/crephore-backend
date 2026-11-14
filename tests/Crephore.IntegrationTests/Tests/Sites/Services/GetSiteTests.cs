using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.IntegrationTests.Factories;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Sites.Services;

public class GetSiteTests(DatabaseFixture fixture)
    : SitesBaseTestClass(fixture)
{
    private static readonly WebsiteMotionDto DefaultMotion = new() { Intro = "shutter", Intensity = "bold" };

    [Fact]
    public async Task ShouldReturnThePublishedWebsiteWithTheEventsDetails_WhenItIsPublished()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, name: "Ana & Marco", subdomain: "ana-and-marco");
        await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split-framed", "Welcome"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Hidden", isVisible: false),
        ], published: true);

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Subdomain.Should().Be("ana-and-marco");
        site.EventName.Should().Be("Ana & Marco");
        site.VenueName.Should().Be("Manila Cathedral");
        site.EventType.Should().Be(EventType.Birthday);
        site.StartsAt.Should().Be(@event.StartsAt);
        site.EndsAt.Should().Be(@event.EndsAt);
        site.Timezone.Should().Be("Asia/Manila");
        site.Theme.FontPairingKey.Should().Be("serif_classic");
        site.Sections.Should().ContainSingle().Which.Content.GetProperty("heading").GetString().Should().Be("Welcome");
    }

    [Fact]
    public async Task ShouldReturnTheEventsLiveProgram_WhenItIsPublished()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        // Added after publishing: guests see it without publishing again
        await EventProgramItemFactory.CreateAsync(@event.Id, "Ceremony", "Dinner");

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Program.Items.Select(i => i.Title).Should().Equal("Ceremony", "Dinner");
        site.Program.Items[1].StartTime.Should().Be("16:00");
    }

    [Fact]
    public async Task ShouldReturnTheEventsLiveVenuesInOrder_WithTheMainOnesName()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        // Added after publishing: guests see them without publishing again
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        venues[0].SortOrder = 1;
        venues[1].SortOrder = 0;
        venues[1].Notes = "Parking in the basement.";
        await Context.SaveChangesAsync();
        await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[0].Id, "Ceremony");

        var site = await SitesService.GetAsync("ana-and-marco");

        site.VenueName.Should().Be("Shangri-La");
        site.Venues.Select(v => v.Name).Should().Equal("Shangri-La", "Manila Cathedral");
        site.Venues[0].Should().BeEquivalentTo(new { Id = venues[1].Id, Notes = "Parking in the basement.", Address = "2 Test Street, Manila", Latitude = (decimal?)null });
        site.Program.Items.Should().ContainSingle().Which.VenueId.Should().Be(venues[0].Id);
    }

    [Fact]
    public async Task ShouldReturnThePhotosOfTheSectionsAndVenuesWithTheirUrls()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var cover = await MediaFactory.CreatePhotoAsync(@event.Id);
        var hotel = await MediaFactory.CreatePhotoAsync(@event.Id);
        var section = EventWebsiteFactory.BuildSection();
        section.Content = $$"""{ "backgroundPhoto": { "mediaId": {{cover.Id}} }, "layoutPhotos": [] }""";
        await EventWebsiteFactory.CreateAsync(@event.Id, [section], published: true);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Shangri-La");
        venues[0].PhotoMediaId = hotel.Id;
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Sections.Single().Content.GetProperty("backgroundPhoto").GetProperty("url").GetString()
            .Should().Be($"{CustomWebApplicationFactory.StoragePublicBaseUrl}/{cover.StorageKey}");
        site.Venues.Single().Photo.Should().BeEquivalentTo(new
        {
            MediaId = hotel.Id,
            Url = $"{CustomWebApplicationFactory.StoragePublicBaseUrl}/{hotel.StorageKey}",
        });
    }

    [Fact]
    public async Task ShouldReturnNoVenuesAndAnEmptyVenueName_WhenTheEventHasNone()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        var site = await SitesService.GetAsync("ana-and-marco");

        site.VenueName.Should().BeEmpty();
        site.Venues.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldReturnAMemorialsLiveLifeDates_WhenItIsPublished()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "lola-ising", type: EventType.Memorial);
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        // Added after publishing: guests see them without publishing again
        @event.BornOn = new DateOnly(1938, 5, 1);
        @event.DiedOn = new DateOnly(2026, 10, 3);
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("lola-ising");

        site.BornOn.Should().Be(new DateOnly(1938, 5, 1));
        site.DiedOn.Should().Be(new DateOnly(2026, 10, 3));
    }

    [Fact]
    public async Task ShouldReturnThePublishedColours_NullWhenTheWebsiteHasNoSecondaryOrAccentColor()
    {
        var owner = await AppUserFactory.CreateAsync();
        var withColours = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(withColours.Id, published: true);
        var withoutColours = await EventFactory.CreateAsync(owner.Id, subdomain: "bea-and-carlo");
        var website = await EventWebsiteFactory.CreateAsync(withoutColours.Id);
        website.SecondaryColor = null;
        website.AccentColor = null;
        website.PublishedContent = website.ToPublishedContent();
        website.PublishedAt = DateTimeOffset.UtcNow;
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");
        var plain = await SitesService.GetAsync("bea-and-carlo");

        site.Theme.Should().BeEquivalentTo(new { PrimaryColor = "#1E2A44", SecondaryColor = "#E8A598", AccentColor = "#C9A227" });
        plain.Theme.Should().BeEquivalentTo(new { PrimaryColor = "#1E2A44", SecondaryColor = (string?)null, AccentColor = (string?)null });
    }

    [Fact]
    public async Task ShouldReturnWhatWasPublished_WhenTheWebsiteWasSavedSince()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, [EventWebsiteFactory.BuildSection(heading: "Published")], published: true);
        website.Sections.Single().Content = """{ "heading": "Saved later", "layoutPhotos": [] }""";
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Sections.Single().Content.GetProperty("heading").GetString().Should().Be("Published");
    }

    [Fact]
    public async Task ShouldReturnThePublishedMotion_WhenItIsPublished()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true,
            motion: new() { Intro = "stamp", Intensity = "subtle" });

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Theme.Motion.Should().BeEquivalentTo(new { Intro = "stamp", Intensity = "subtle" });
    }

    [Fact]
    public async Task ShouldReturnTheDefaultMotion_WhenPublishedBeforeMotionExisted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        website.PublishedContent = EventWebsiteFactory.WithoutMotion(website.PublishedContent!);
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Theme.Motion.Should().BeEquivalentTo(DefaultMotion);
    }

    [Fact]
    public async Task ShouldReturnTheDefaultMotion_WhenPublishedWithTheOldMotionShape()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        website.PublishedContent = EventWebsiteFactory.WithOldMotion(website.PublishedContent!, transition: "shutter", animation: "text-lines");
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Theme.Motion.Should().BeEquivalentTo(DefaultMotion);
    }

    [Fact]
    public async Task ShouldReturnTheDefaultMotion_WhenPublishedWithTheScrollAnimations()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, published: true);
        website.PublishedContent = EventWebsiteFactory.WithScrollMotion(website.PublishedContent!);
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Theme.Motion.Should().BeEquivalentTo(DefaultMotion);
    }

    [Fact]
    public async Task ShouldFindTheWebsite_WhateverTheCaseOfTheAddress()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        var site = await SitesService.GetAsync(" Ana-And-Marco ");

        site.Subdomain.Should().Be("ana-and-marco");
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenTheWebsiteIsNotPublished()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var act = async () => await SitesService.GetAsync("ana-and-marco");

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Website not found.");
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenNoEventHasTheAddress()
    {
        var act = async () => await SitesService.GetAsync("nobody-here");

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Website not found.");
    }
}
