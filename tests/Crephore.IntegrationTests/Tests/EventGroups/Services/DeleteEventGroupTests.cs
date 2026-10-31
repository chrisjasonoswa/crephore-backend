using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventGroups.Services;

public class DeleteEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldDeleteTheGroupAndUngroupItsEvents_WithoutDeletingThem()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        await EventGroupsService.DeleteAsync(group.Id, owner.Id);

        (await Context.EventGroups.AsNoTracking().AnyAsync()).Should().BeFalse();
        (await Context.Events.AsNoTracking().AnyAsync(e => e.Id == @event.Id)).Should().BeTrue();
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id && eu.UserId == owner.Id)).EventGroupId.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenGroupBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);

        var act = async () => await EventGroupsService.DeleteAsync(group.Id, otherUser.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event group not found.");
    }
}
