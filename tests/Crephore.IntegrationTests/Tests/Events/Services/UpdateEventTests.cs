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
        Description = "  Class of 2017  ",
        Type = EventType.Other,
        VenueName = "Park Hyatt",
        VenueAddress = "Shinjuku, Tokyo",
        StartsAt = Start,
        EndsAt = Start.AddHours(4),
        Timezone = "Asia/Tokyo",
        Subdomain = subdomain,
        Timeline = ["Welcome", " Dinner "],
    };

    [Fact]
    public async Task ShouldReplaceEveryField_IncludingTheTimeline()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id, "Birthday", timeline: ["Opening"]);

        var result = await EventsService.UpdateAsync(@event.Id, owner.Id, BuildRequest("class-2017"));

        result.Name.Should().Be("Reunion");
        result.Description.Should().Be("Class of 2017");
        result.Timeline.Should().Equal("Welcome", "Dinner");

        var inDb = await Context.Events.AsNoTracking().SingleAsync(e => e.Id == @event.Id);
        inDb.Subdomain.Should().Be("class-2017");
        inDb.Timezone.Should().Be("Asia/Tokyo");
        // 6:00 PM in Tokyo (UTC+9) is 9:00 AM UTC
        inDb.StartsAt.Should().Be(new DateTimeOffset(2027, 9, 4, 9, 0, 0, TimeSpan.Zero));
        inDb.Timeline.Should().Equal("Welcome", "Dinner");
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
    public async Task ShouldSetAndClearTheUsersGroup()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var @event = await EventFactory.CreateAsync(owner.Id);
        var request = BuildRequest(@event.Subdomain);

        request.EventGroupId = group.Id;
        (await EventsService.UpdateAsync(@event.Id, owner.Id, request)).EventGroup!.Name.Should().Be("Family");

        request.EventGroupId = null;
        (await EventsService.UpdateAsync(@event.Id, owner.Id, request)).EventGroup.Should().BeNull();
        (await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == @event.Id)).EventGroupId.Should().BeNull();
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
