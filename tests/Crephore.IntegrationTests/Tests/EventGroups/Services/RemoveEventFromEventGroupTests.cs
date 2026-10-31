using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventGroups.Services;

public class RemoveEventFromEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldUngroupTheEventWithoutDeletingIt()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        var result = await EventGroupsService.RemoveEventAsync(group.Id, @event.Id, owner.Id);

        result.EventCount.Should().Be(0);
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeTrue();
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id && eu.UserId == owner.Id)).EventGroupId.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventIsNotInThatGroup()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var otherGroup = await EventGroupFactory.CreateAsync(owner.Id);
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: otherGroup.Id);

        var act = async () => await EventGroupsService.RemoveEventAsync(group.Id, @event.Id, owner.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event is not in this group.");
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id && eu.UserId == owner.Id)).EventGroupId.Should().Be(otherGroup.Id);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenGroupBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var othersGroup = await EventGroupFactory.CreateAsync(otherUser.Id);
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventGroupsService.RemoveEventAsync(othersGroup.Id, @event.Id, owner.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event group not found.");
    }
}
