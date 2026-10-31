using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Factories.Entities;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Sites.Services;

public class GetSiteTests(DatabaseFixture fixture)
    : SitesBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnThePublishedWebsiteWithTheEventsDetails_WhenItIsPublished()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, name: "Ana & Marco", subdomain: "ana-and-marco");
        await EventWebsiteFactory.CreateAsync(@event.Id,
        [
            EventWebsiteFactory.BuildSection("hero", "split", "Welcome"),
            EventWebsiteFactory.BuildSection("countdown", "boxes", "Hidden", isVisible: false),
        ], published: true);

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Subdomain.Should().Be("ana-and-marco");
        site.EventName.Should().Be("Ana & Marco");
        site.StartsAt.Should().Be(@event.StartsAt);
        site.Timezone.Should().Be("Asia/Manila");
        site.Theme.FontPairingKey.Should().Be("serif_classic");
        site.Sections.Should().ContainSingle().Which.Content.GetProperty("title").GetString().Should().Be("Welcome");
    }

    [Fact]
    public async Task ShouldReturnWhatWasPublished_WhenTheWebsiteWasSavedSince()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "ana-and-marco");
        var website = await EventWebsiteFactory.CreateAsync(@event.Id, [EventWebsiteFactory.BuildSection(title: "Published")], published: true);
        website.Sections.Single().Content = """{ "title": "Saved later", "layoutPhotos": [] }""";
        await Context.SaveChangesAsync();

        var site = await SitesService.GetAsync("ana-and-marco");

        site.Sections.Single().Content.GetProperty("title").GetString().Should().Be("Published");
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
