using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Events.Services;

public class DeleteEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldDeleteTheEventAndItsUsers_WhenOwnerDeletesIt()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var other = await EventFactory.CreateAsync(owner.Id);

        await EventsService.DeleteAsync(@event.Id, owner.Id);

        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeFalse();
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == other.Id)).Should().BeTrue();
        (await Context.EventUsers.AsNoTracking().AnyAsync(eu => eu.EventId == @event.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldKeepTheGroup_WhenAGroupedEventIsDeleted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        await EventsService.DeleteAsync(@event.Id, owner.Id);

        (await Context.EventGroups.AsNoTracking().AnyAsync(g => g.Id == group.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventsService.DeleteAsync(@event.Id, otherUser.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event not found.");
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);

        var act = async () => await EventsService.DeleteAsync(@event.Id, member.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeTrue();
    }
}
