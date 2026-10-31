using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Events.Services;

public class GetEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnTheEvent_WhenOwnerRequestsIt()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, "Birthday", timeline: ["Opening", "Dinner"]);

        var result = await EventsService.GetAsync(@event.Id, owner.Id);

        result.Name.Should().Be("Birthday");
        result.Subdomain.Should().Be(@event.Subdomain);
        result.Timeline.Should().Equal("Opening", "Dinner");
        result.EventGroup.Should().BeNull();
        result.Access.Should().Equal(EventAccess.Owner);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventsService.GetAsync(@event.Id, otherUser.Id);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event not found.");
    }

    [Fact]
    public async Task ShouldReturnTheUsersOwnAccessAndGroup_WhenTheEventIsSharedWithThem()
    {
        var owner = await AppUserFactory.CreateAsync();
        var ownersGroup = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: ownersGroup.Id);
        var member = await AppUserFactory.CreateAsync();
        var membersGroup = await EventGroupFactory.CreateAsync(member.Id, "Friends");
        await EventUserFactory.CreateAsync(@event.Id, member.Id, eventGroupId: membersGroup.Id);

        var result = await EventsService.GetAsync(@event.Id, member.Id);

        result.Access.Should().BeEmpty();
        result.EventGroup!.Name.Should().Be("Friends");
    }
}
