using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.Events;
using Crephore.Api.Features.Events.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Events.Services;

public class CreateEventTests(DatabaseFixture fixture)
    : EventsBaseTestClass(fixture)
{
    // Wall-clock time at the venue (Asia/Manila, UTC+8), as the web app sends it
    private static readonly DateTime Start = new(2027, 6, 12, 15, 0, 0);

    private static CreateEventRequestDto BuildValidRequest() => new()
    {
        Name = "Ana & Marco",
        Description = "Our wedding",
        Type = EventType.Wedding,
        VenueName = "Taal Vista",
        VenueAddress = "Tagaytay, Philippines",
        VenueLatitude = null,
        VenueLongitude = null,
        VenueGooglePlaceId = null,
        StartsAt = Start,
        EndsAt = Start.AddHours(8),
        Timezone = "Asia/Manila",
        Subdomain = $"ana-and-marco-{Guid.NewGuid().ToString("N")[..6]}",
        Timeline = ["Ceremony", "Reception"],
    };

    [Fact]
    public async Task ShouldCreateTheEventWithItsTimelineInOrder_WhenRequestIsValid()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();

        var result = await EventsService.CreateAsync(owner.Id, request);

        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Ana & Marco");
        result.Type.Should().Be(EventType.Wedding);
        result.VenueLatitude.Should().BeNull();
        result.Access.Should().Equal(EventAccess.Owner);
        result.Timeline.Should().Equal("Ceremony", "Reception");

        var inDb = await Context.Events.AsNoTracking().FirstAsync(e => e.Id == result.Id);
        inDb.Timeline.Should().Equal("Ceremony", "Reception");
        inDb.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // The creator is the only user, with Owner access
        var eventUser = await Context.EventUsers.AsNoTracking().SingleAsync(eu => eu.EventId == result.Id);
        eventUser.UserId.Should().Be(owner.Id);
        eventUser.Access.Should().Equal(EventAccess.Owner);
    }

    [Fact]
    public async Task ShouldStoreTheTimesInUtc_UsingTheEventsTimezone()
    {
        var owner = await AppUserFactory.CreateAsync();

        var result = await EventsService.CreateAsync(owner.Id, BuildValidRequest());

        // 3:00 PM in Manila (UTC+8) is 7:00 AM UTC
        var inDb = await Context.Events.AsNoTracking().SingleAsync(e => e.Id == result.Id);
        inDb.Timezone.Should().Be("Asia/Manila");
        inDb.StartsAt.Should().Be(new DateTimeOffset(2027, 6, 12, 7, 0, 0, TimeSpan.Zero));
        inDb.EndsAt.Should().Be(new DateTimeOffset(2027, 6, 12, 15, 0, 0, TimeSpan.Zero));
        result.Timezone.Should().Be("Asia/Manila");
    }

    [Fact]
    public async Task ShouldStoreAccessAsAJsonArrayOfNames()
    {
        var owner = await AppUserFactory.CreateAsync();

        var result = await EventsService.CreateAsync(owner.Id, BuildValidRequest());

        var stored = await Context.Database
            .SqlQuery<string>($"SELECT Access AS Value FROM event_users WHERE EventId = {result.Id}")
            .SingleAsync();
        stored.Should().Be("""["Owner"]""");
    }

    [Fact]
    public async Task ShouldNormalizeTheSubdomainToLowercase()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Subdomain = "  Birthday-Ni-CJ-2021  ";

        var result = await EventsService.CreateAsync(owner.Id, request);

        result.Subdomain.Should().Be("birthday-ni-cj-2021");
    }

    [Fact]
    public async Task ShouldAddTheEventToTheGroup_WhenEventGroupIdIsProvided()
    {
        var owner = await AppUserFactory.CreateAsync();
        var group = await EventGroupFactory.CreateAsync(owner.Id, "Family");
        var request = BuildValidRequest();
        request.EventGroupId = group.Id;

        var result = await EventsService.CreateAsync(owner.Id, request);

        result.EventGroup!.Id.Should().Be(group.Id);
        result.EventGroup.Name.Should().Be("Family");
    }

    [Fact]
    public async Task ShouldCreateTheEventWithoutTimeline_WhenNoneIsGiven()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Timeline = [];

        var result = await EventsService.CreateAsync(owner.Id, request);

        result.Timeline.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-starts-with-hyphen")]
    [InlineData("ends-with-hyphen-")]
    [InlineData("has spaces")]
    [InlineData("has_underscore")]
    public async Task ShouldThrowValidationException_WhenSubdomainIsInvalid(string subdomain)
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Subdomain = subdomain;

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["subdomain"]
            .Should().Contain("The website address may only contain lowercase letters, numbers and hyphens.");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenSubdomainIsTaken_EvenByAnotherUser()
    {
        var otherUser = await AppUserFactory.CreateAsync();
        await EventFactory.CreateAsync(otherUser.Id, subdomain: "taken-subdomain");
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Subdomain = "Taken-Subdomain";

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["subdomain"]
            .Should().Contain("This website address is already taken.");
    }

    [Fact]
    public async Task ShouldRejectTheSecond_WhenTwoRequestsUseTheSameSubdomainAtOnce()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();

        var results = await RunConcurrentlyAsync<IEventsService>(2, service => service.CreateAsync(owner.Id, request));

        // Whichever request loses gets the field error, never a 500 from the unique index
        results.Should().ContainSingle(exception => exception == null);
        results.Single(exception => exception != null).Should().BeOfType<AppValidationException>()
            .Which.Errors!["subdomain"].Should().Contain("This website address is already taken.");
        (await Context.Events.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenEventEndsBeforeItStarts()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.EndsAt = Start.AddHours(-1);

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKey("endsAt");
    }

    [Fact]
    public async Task ShouldTrimTimelineItems()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Timeline = ["  Opening ", "Dinner"];

        var result = await EventsService.CreateAsync(owner.Id, request);

        result.Timeline.Should().Equal("Opening", "Dinner");
    }

    [Theory]
    [InlineData("   ", "Each timeline item must be filled in.")]
    [InlineData(null, "Each timeline item must be filled in.")]
    public async Task ShouldThrowValidationException_WhenATimelineItemIsEmpty(string? item, string message)
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Timeline = ["Opening", item!];

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["timeline"].Should().Contain(message);
        (await Context.Events.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenEventGroupBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var othersGroup = await EventGroupFactory.CreateAsync(otherUser.Id);
        var request = BuildValidRequest();
        request.EventGroupId = othersGroup.Id;

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKey("eventGroupId");
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("Pacific Standard Time")] // Windows ids are not accepted; browsers only know IANA ids
    public async Task ShouldThrowValidationException_WhenTimezoneIsNotAnIanaTimeZone(string timezone)
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Timezone = timezone;

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors.Should().ContainKey("timezone");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenATimeHasAnOffset()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.StartsAt = DateTime.SpecifyKind(Start, DateTimeKind.Utc);

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["startsAt"]
            .Should().Contain("The start must be a local date and time without an offset.");
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenATimeDoesNotExistInTheTimezone()
    {
        var owner = await AppUserFactory.CreateAsync();
        var request = BuildValidRequest();
        request.Timezone = "America/New_York";
        request.Timeline = [];
        // Clocks jump from 2:00 to 3:00 AM on March 14, 2027
        request.StartsAt = new DateTime(2027, 3, 14, 2, 30, 0);
        request.EndsAt = new DateTime(2027, 3, 14, 6, 0, 0);

        var act = async () => await EventsService.CreateAsync(owner.Id, request);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["startsAt"]
            .Should().Contain("The start does not exist in this timezone (the clocks skip it).");
    }
}
