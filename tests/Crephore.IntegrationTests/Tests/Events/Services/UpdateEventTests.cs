using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Events.Services;

public class UpdateEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    // Wall-clock time at the venue (Asia/Tokyo, UTC+9)
    private static readonly DateTime Start = new(2027, 9, 4, 18, 0, 0);

    private static UpdateEventRequestDto BuildRequest(string subdomain) => new()
    {
        Name = "Reunion",
        Type = EventType.Other,
        StartsAt = Start,
        EndsAt = Start.AddHours(4),
        Timezone = "Asia/Tokyo",
        Subdomain = subdomain,
    };

    [Fact]
    public async Task ShouldReplaceEveryField()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, "Birthday");

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, BuildRequest("class-2017"));

        result.Name.Should().Be("Reunion");

        var inDb = await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        inDb.Subdomain.Should().Be("class-2017");
        inDb.Timezone.Should().Be("Asia/Tokyo");
        // 6:00 PM in Tokyo (UTC+9) is 9:00 AM UTC
        inDb.StartsAt.Should().Be(new DateTimeOffset(2027, 9, 4, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ShouldKeepTheVenues_AndReturnTheMainVenuesName()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventVenueFactory.CreateAsync(@event.Id, "Park Hyatt", "Golden Gai");

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(@event.Subdomain));

        result.VenueName.Should().Be("Park Hyatt");
        (await Context.EventVenues.CountAsync(v => v.EventId == @event.Id)).Should().Be(2);
    }

    [Fact]
    public async Task ShouldSaveAMemorialsLifeDates()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(@event.Subdomain);
        request.Type = EventType.Memorial;
        request.BornOn = new DateOnly(1948, 2, 14);
        request.DiedOn = new DateOnly(2026, 10, 3);

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, request);

        result.BornOn.Should().Be(new DateOnly(1948, 2, 14));
        result.DiedOn.Should().Be(new DateOnly(2026, 10, 3));
        var inDb = await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        inDb.BornOn.Should().Be(new DateOnly(1948, 2, 14));
        inDb.DiedOn.Should().Be(new DateOnly(2026, 10, 3));
    }

    [Fact]
    public async Task ShouldClearTheLifeDates_WhenTheEventIsNotAMemorial()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, type: EventType.Memorial);
        @event.BornOn = new DateOnly(1948, 2, 14);
        await Context.SaveChangesAsync();
        var request = BuildRequest(@event.Subdomain);
        // Sent by mistake with another type: never kept
        request.DiedOn = new DateOnly(2026, 10, 3);

        await EventsService.UpdateAsync(@event.Id, owner.Id, request);

        var inDb = await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        inDb.BornOn.Should().BeNull();
        inDb.DiedOn.Should().BeNull();
    }

    [Fact]
    public async Task ShouldAllowKeepingItsOwnSubdomain()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, subdomain: "my-event");

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, BuildRequest("my-event"));

        result.Subdomain.Should().Be("my-event");
    }

    [Fact]
    public async Task ShouldKeepTheUsersGroup()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var @event = await EventFactory.CreateAsync(owner.Id, eventGroupId: group.Id);

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, BuildRequest(@event.Subdomain));

        // Grouping is done through the groups endpoints, never by saving the event
        result.EventGroup!.Name.Should().Be("Family");
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id)).EventGroupId.Should().Be(group.Id);
    }

    [Fact]
    public async Task ShouldSaveAnEventWithoutAnEnd()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(@event.Subdomain);
        request.EndsAt = null;

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, request);

        result.EndsAt.Should().BeNull();
        (await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id)).EndsAt.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenSubdomainIsTakenByAnotherEvent()
    {
        var owner = await AppUserFactory.CreateAsync();
        await EventFactory.CreateAsync(owner.Id, subdomain: "taken");
        var @event = await EventFactory.CreateAsync(owner.Id, "Original");

        var act = async () => await EventsService.UpdateAsync(@event.Id, owner.Id, BuildRequest("taken"));

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["subdomain"]
            .Should().Contain("This website address is already taken.");
        (await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id)).Name.Should().Be("Original");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenAMemorialsDateOfPassingIsBeforeItsDateOfBirth()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(@event.Subdomain);
        request.Type = EventType.Memorial;
        request.BornOn = new DateOnly(1948, 2, 14);
        request.DiedOn = new DateOnly(1948, 2, 13);

        var act = async () => await EventsService.UpdateAsync(@event.Id, owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["diedOn"]
            .Should().Contain("The date of passing must not be before the date of birth.");
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await EventsService.UpdateAsync(@event.Id, otherUser.Id, BuildRequest("someone-elses"));

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().Be("Event not found.");
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, "Original");
        await EventUserFactory.CreateAsync(@event.Id, member.Id);

        var act = async () => await EventsService.UpdateAsync(@event.Id, member.Id, BuildRequest(@event.Subdomain));

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id)).Name.Should().Be("Original");
    }
}
