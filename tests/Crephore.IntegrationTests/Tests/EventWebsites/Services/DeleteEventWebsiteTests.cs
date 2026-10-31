using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventWebsites.Services;

public class DeleteEventWebsiteTests(DatabaseFixture fixture)
    : EventWebsitesBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldDeleteTheWebsiteAndItsSections_WhenOwnerDeletesIt()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var otherEvent = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);
        var other = await EventWebsiteFactory.CreateAsync(otherEvent.Id);

        await EventWebsitesService.DeleteAsync(@event.Id, owner.Id);

        (await Context.EventWebsites.AsNoTracking().Select(w => w.Id).ToListAsync()).Should().Equal(other.Id);
        (await Context.WebsiteSections.AsNoTracking().AllAsync(s => s.EventWebsiteId == other.Id)).Should().BeTrue();
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldDeleteTheWebsite_WhenItsEventIsDeleted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        await EventsService.DeleteAsync(@event.Id, owner.Id);

        (await Context.EventWebsites.CountAsync()).Should().Be(0);
        (await Context.WebsiteSections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenTheEventHasNoWebsite()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventWebsitesService.DeleteAsync(@event.Id, owner.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("This event has no website yet.");
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);
        await EventWebsiteFactory.CreateAsync(@event.Id);

        var act = async () => await EventWebsitesService.DeleteAsync(@event.Id, member.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.EventWebsites.CountAsync()).Should().Be(1);
    }
}
