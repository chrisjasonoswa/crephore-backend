using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventPrograms.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventPrograms.Services;

public class UpdateEventProgramTests(DatabaseFixture fixture)
    : EventProgramsBaseTestClass(fixture)
{
    private static UpdateEventProgramItemRequestDto BuildItem(string title = "Ceremony", int? id = null) => new()
    {
        Id = id,
        Date = "2027-06-12",
        StartTime = "15:00",
        EndTime = "16:00",
        Title = title,
    };

    private static UpdateEventProgramRequestDto BuildRequest(params UpdateEventProgramItemRequestDto[] items) => new()
    {
        Items = [.. items],
    };

    [Fact]
    public async Task ShouldSaveTheItemsInTheRequestsOrder_TrimmedAndWithBlankTextAsNull()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var dinner = BuildItem("  Dinner  ");
        dinner.StartTime = "21:00";
        dinner.EndTime = "01:00";
        dinner.Description = "  Black tie  ";
        dinner.Speakers = "Ana Lim, Paolo Tan";

        var program = await EventProgramsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildItem(), dinner));

        program.Items.Select(i => i.Title).Should().Equal("Ceremony", "Dinner");
        program.Items.Should().OnlyContain(i => i.Id > 0);

        var saved = await Context.EventProgramItems.AsNoTracking().Where(i => i.EventId == @event.Id).OrderBy(i => i.SortOrder).ToListAsync();
        saved.Select(i => i.SortOrder).Should().Equal(0, 1);
        // Ends after midnight: an end earlier than the start
        saved[1].Should().BeEquivalentTo(new
        {
            Date = new DateOnly(2027, 6, 12),
            StartTime = (TimeOnly?)new TimeOnly(21, 0),
            EndTime = (TimeOnly?)new TimeOnly(1, 0),
            Title = "Dinner",
            Description = "Black tie",
            VenueId = (int?)null,
            Speakers = "Ana Lim, Paolo Tan",
        });
    }

    [Fact]
    public async Task ShouldSaveAnItemWithOnlyADateAndTitle()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var program = await EventProgramsService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(new UpdateEventProgramItemRequestDto { Date = "2027-06-13", Title = "Brunch" }));

        program.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Date = "2027-06-13", StartTime = (string?)null, EndTime = (string?)null, Title = "Brunch",
        });
    }

    [Fact]
    public async Task ShouldUpdateSavedItemsByIdAndDeleteTheOnesLeftOut()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var items = await EventProgramItemFactory.CreateAsync(@event.Id, "Ceremony", "Cocktails", "Dinner");

        // Dinner first, Ceremony renamed, Cocktails left out, a new item at the end
        await EventProgramsService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildItem("Dinner", items[2].Id), BuildItem("Mass", items[0].Id), BuildItem("Dancing")));

        var saved = await Context.EventProgramItems.AsNoTracking().Where(i => i.EventId == @event.Id).OrderBy(i => i.SortOrder).ToListAsync();
        saved.Select(i => i.Title).Should().Equal("Dinner", "Mass", "Dancing");
        saved[0].Id.Should().Be(items[2].Id);
        saved[1].Id.Should().Be(items[0].Id);
        (await Context.EventProgramItems.AnyAsync(i => i.Id == items[1].Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldSaveEachItemsVenue_WhenItIsOneOfTheEventsVenues()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        var items = await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[0].Id, "Ceremony");
        var ceremony = BuildItem("Ceremony", items[0].Id);
        ceremony.VenueId = venues[1].Id;
        var reception = BuildItem("Reception");
        reception.VenueId = venues[0].Id;
        // Without a venue, e.g. a livestream
        var livestream = BuildItem("Livestream");

        var program = await EventProgramsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(ceremony, reception, livestream));

        program.Items.Select(i => i.VenueId).Should().Equal(venues[1].Id, venues[0].Id, null);
        var saved = await Context.EventProgramItems.AsNoTracking().Where(i => i.EventId == @event.Id).OrderBy(i => i.SortOrder).ToListAsync();
        saved.Select(i => i.VenueId).Should().Equal(venues[1].Id, venues[0].Id, null);
    }

    [Fact]
    public async Task ShouldRemoveEveryItem_WhenTheRequestHasNone()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventProgramItemFactory.CreateAsync(@event.Id, "Ceremony", "Dinner");

        var program = await EventProgramsService.UpdateAsync(@event.Id, owner.Id, BuildRequest());

        program.Items.Should().BeEmpty();
        (await Context.EventProgramItems.CountAsync(i => i.EventId == @event.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationErrorsKeyedByItem_WhenItemsAreInvalid()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var noDateOrTitle = new UpdateEventProgramItemRequestDto { Title = "  " };
        var badTimes = new UpdateEventProgramItemRequestDto { Date = "2027-02-30", StartTime = "3pm", Title = "Party" };
        var endWithoutStart = new UpdateEventProgramItemRequestDto { Date = "2027-06-12", EndTime = "16:00", Title = new string('a', 151) };

        var act = () => EventProgramsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildItem(), noDateOrTitle, badTimes, endWithoutStart));

        var exception = (await act.Should().ThrowAsync<AppValidationException>()).Which;
        exception.Errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["items[1].date"] = ["Date is required."],
            ["items[1].title"] = ["Title is required."],
            ["items[2].date"] = ["Date is not a valid date."],
            ["items[2].startTime"] = ["Start time must be a time like 15:00."],
            ["items[3].endTime"] = ["Add a start time first."],
            ["items[3].title"] = ["Title must be 150 characters or fewer."],
        });
        (await Context.EventProgramItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationErrorKeyedByItem_WhenTheVenueIsNotOneOfTheEventsVenues()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventVenueFactory.CreateAsync(@event.Id);
        var other = await EventFactory.CreateAsync(owner.Id);
        var otherVenues = await EventVenueFactory.CreateAsync(other.Id);
        var item = BuildItem();
        item.VenueId = otherVenues[0].Id;

        var act = () => EventProgramsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildItem(), item));

        var exception = (await act.Should().ThrowAsync<AppValidationException>()).Which;
        exception.Errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["items[1].venueId"] = ["Pick one of the event's venues."],
        });
        (await Context.EventProgramItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationError_WhenAnItemIsNotOneOfThisEvents()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var other = await EventFactory.CreateAsync(owner.Id);
        var otherItems = await EventProgramItemFactory.CreateAsync(other.Id);

        var act = () => EventProgramsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildItem(id: otherItems[0].Id)));

        var exception = (await act.Should().ThrowAsync<AppValidationException>()).Which;
        exception.Errors!["items"].Should().Equal("Item 1 is no longer in the program. Reload the page to see the latest version.");
    }

    [Fact]
    public async Task ShouldThrowForbidden_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var guest = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, guest.Id);

        var act = () => EventProgramsService.UpdateAsync(@event.Id, guest.Id, BuildRequest(BuildItem()));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ShouldThrowNotFound_WhenUserHasNoAccessToTheEvent()
    {
        var owner = await AppUserFactory.CreateAsync();
        var stranger = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = () => EventProgramsService.UpdateAsync(@event.Id, stranger.Id, BuildRequest(BuildItem()));

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
