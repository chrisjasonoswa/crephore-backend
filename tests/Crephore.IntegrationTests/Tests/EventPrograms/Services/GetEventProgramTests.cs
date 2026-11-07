using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventPrograms.Services;

public class GetEventProgramTests(DatabaseFixture fixture)
    : EventProgramsBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnTheItemsInTheirOrder_WithTheirDateTimesAndVenue()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id);
        await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[0].Id, "Ceremony", "Cocktails");

        var program = await EventProgramsService.GetAsync(@event.Id, owner.Id);

        program.Items.Select(i => i.Title).Should().Equal("Ceremony", "Cocktails");
        program.Items[0].Should().BeEquivalentTo(new { Date = "2027-06-12", StartTime = "15:00", EndTime = (string?)null, VenueId = (int?)venues[0].Id });
    }

    [Fact]
    public async Task ShouldReturnAnEmptyProgram_WhenItHasNoItems()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var program = await EventProgramsService.GetAsync(@event.Id, owner.Id);

        program.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldReturnTheProgram_WhenUserHasAccessButIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var guest = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, guest.Id);
        await EventProgramItemFactory.CreateAsync(@event.Id);

        var program = await EventProgramsService.GetAsync(@event.Id, guest.Id);

        program.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ShouldThrowNotFound_WhenUserHasNoAccessToTheEvent()
    {
        var owner = await AppUserFactory.CreateAsync();
        var stranger = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = () => EventProgramsService.GetAsync(@event.Id, stranger.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
