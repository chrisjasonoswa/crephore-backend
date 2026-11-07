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
        var @event = await EventFactory.CreateAsync(owner.Id, "Birthday");

        var result = await EventsService.GetAsync(@event.Id, owner.Id);

        result.Name.Should().Be("Birthday");
        result.Subdomain.Should().Be(@event.Subdomain);
        result.EventGroup.Should().BeNull();
        result.Access.Should().Equal(EventAccess.Owner);
    }

    [Fact]
    public async Task ShouldReturnTheMainVenuesName_WhenTheEventHasVenues()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        // The host's order, not the order they were added in
        venues[0].SortOrder = 1;
        venues[1].SortOrder = 0;
        await Context.SaveChangesAsync();

        var result = await EventsService.GetAsync(@event.Id, owner.Id);

        result.VenueName.Should().Be("Shangri-La");
    }

    [Fact]
    public async Task ShouldReturnAnEmptyVenueName_WhenTheEventHasNoVenues()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var result = await EventsService.GetAsync(@event.Id, owner.Id);

        result.VenueName.Should().BeEmpty();
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
