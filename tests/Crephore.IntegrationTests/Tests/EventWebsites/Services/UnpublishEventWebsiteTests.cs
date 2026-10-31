using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class UnpublishEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldStopShowingItToGuestsAndKeepTheSavedWebsite_WhenOwnerUnpublishes()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        var website = await EventWebsitesService.UnpublishAsync(@event.Id, owner.Id);

        website.PublishedAt.Should().BeNull();
        website.Sections.Should().ContainSingle();
        var saved = await Context.EventWebsites.AsNoTracking().SingleAsync();
        saved.PublishedContent.Should().BeNull();
        saved.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenTheEventHasNoWebsite()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventWebsitesService.UnpublishAsync(@event.Id, owner.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id, published: true);

        var act = async () => await EventWebsitesService.UnpublishAsync(@event.Id, member.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.EventWebsites.AsNoTracking().SingleAsync()).PublishedContent.Should().NotBeNull();
    }
}
