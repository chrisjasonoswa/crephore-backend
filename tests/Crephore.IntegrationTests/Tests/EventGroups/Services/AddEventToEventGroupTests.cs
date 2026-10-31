using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventGroups.Services;

public class AddEventToEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldAddTheEventToTheGroup()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var @event = await EventFactory.CreateAsync(owner.Id, "Birthday");

        var result = await EventGroupsService.AddEventAsync(group.Id, @event.Id, owner.Id);

        result.EventCount.Should().Be(1);
        result.Events.Single().Name.Should().Be("Birthday");
        result.Events.Single().Type.Should().Be(@event.Type);
    }

    [Fact]
    public async Task ShouldMoveTheEventOutOfItsCurrentGroup()
    {
        var owner = await AppUserFactory.CreateAsync();
        var from = await EventGroupFactory.CreateAsync(owner.Id, "From");
        var to = await EventGroupFactory.CreateAsync(owner.Id, "To");
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: from.Id);

        await EventGroupsService.AddEventAsync(to.Id, @event.Id, owner.Id);

        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id && eu.UserId == owner.Id)).EventGroupId.Should().Be(to.Id);
    }

    [Fact]
    public async Task ShouldOnlyGroupTheEventForThisUser_WhenTheEventIsShared()
    {
        var owner = await AppUserFactory.CreateAsync();
        var ownersGroup = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: ownersGroup.Id);
        var member = await AppUserFactory.CreateAsync();
        await EventUserFactory.CreateAsync(@event.Id, member.Id);
        var membersGroup = await EventGroupFactory.CreateAsync(member.Id, "Friends");

        var result = await EventGroupsService.AddEventAsync(membersGroup.Id, @event.Id, member.Id);

        result.Events.Single().Id.Should().Be(@event.Id);
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id && eu.UserId == member.Id)).EventGroupId.Should().Be(membersGroup.Id);
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id && eu.UserId == owner.Id)).EventGroupId.Should().Be(ownersGroup.Id);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id);
        var othersEvent = await EventFactory.CreateAsync(otherUser.Id);

        var act = async () => await EventGroupsService.AddEventAsync(group.Id, othersEvent.Id, owner.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event not found.");
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenGroupBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var othersGroup = await EventGroupFactory.CreateAsync(otherUser.Id);
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventGroupsService.AddEventAsync(othersGroup.Id, @event.Id, owner.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event group not found.");
    }
}
