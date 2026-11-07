using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.EventVenues.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.EventVenues.Services;

public class UpdateEventVenuesTests(DatabaseFixture fixture)
    : EventVenuesBaseTestClass(fixture)
{
    private static UpdateEventVenueItemDto BuildVenue(string name = "Manila Cathedral", int? id = null) => new()
    {
        Id = id,
        Name = name,
    };

    private static UpdateEventVenuesRequestDto BuildRequest(params UpdateEventVenueItemDto[] venues) => new()
    {
        Venues = [.. venues],
    };

    // Saves the request and returns the errors it was refused with
    private async Task<Dictionary<string, List<string>>> SaveInvalidAsync(int eventId, int userId, params UpdateEventVenueItemDto[] venues)
    {
        var act = () => EventVenuesService.UpdateAsync(eventId, userId, BuildRequest(venues));
        return (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!;
    }

    [Fact]
    public async Task ShouldSaveTheVenuesInTheRequestsOrder_TrimmedAndWithBlankTextAsNull()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var hotel = new UpdateEventVenueItemDto
        {
            Name = "  Shangri-La  ",
            Address = "  5th Ave, Taguig  ",
            GoogleMapsUrl = " https://www.google.com/maps/place/Shangri-La/@14.55,121.05,17z ",
            AppleMapsUrl = "https://maps.apple.com/?ll=14.55,121.05",
            // Stored with 6 decimals
            Latitude = 14.5512345678m,
            Longitude = 121.0512344m,
            PhotoUrl = "http://localhost:5180/uploads/events/1/hotel.jpg",
            Notes = "  Take the main entrance.\nParking in the basement, free for guests.  ",
        };
        var church = BuildVenue();
        church.Address = "   ";
        church.Notes = " \n ";

        var result = await EventVenuesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(hotel, church));

        result.Select(v => v.Name).Should().Equal("Shangri-La", "Manila Cathedral");
        result.Should().OnlyContain(v => v.Id > 0);

        var saved = await Context.EventVenues.AsNoTracking().Where(v => v.EventId == @event.Id).OrderBy(v => v.SortOrder).ToListAsync();
        saved.Select(v => v.SortOrder).Should().Equal(0, 1);
        saved[0].Should().BeEquivalentTo(new
        {
            Name = "Shangri-La",
            Address = "5th Ave, Taguig",
            GoogleMapsUrl = "https://www.google.com/maps/place/Shangri-La/@14.55,121.05,17z",
            AppleMapsUrl = "https://maps.apple.com/?ll=14.55,121.05",
            Latitude = (decimal?)14.551235m,
            Longitude = (decimal?)121.051234m,
            PhotoUrl = "http://localhost:5180/uploads/events/1/hotel.jpg",
            Notes = "Take the main entrance.\nParking in the basement, free for guests.",
        });
        saved[1].Should().BeEquivalentTo(new
        {
            Address = (string?)null, GoogleMapsUrl = (string?)null, Latitude = (decimal?)null, Notes = (string?)null,
        });
        // Returned as saved
        result[0].Should().BeEquivalentTo(saved[0], options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task ShouldUpdateSavedVenuesByIdAddNewOnesAndDeleteTheOnesLeftOut_InTheRequestsOrder()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Casa Roces", "Shangri-La");

        // Shangri-La first, the cathedral renamed, Casa Roces left out, a new venue at the end
        var result = await EventVenuesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildVenue("Shangri-La", venues[2].Id), BuildVenue("San Agustin Church", venues[0].Id), BuildVenue("Fort Santiago")));

        var saved = await Context.EventVenues.AsNoTracking().Where(v => v.EventId == @event.Id).OrderBy(v => v.SortOrder).ToListAsync();
        saved.Select(v => v.Name).Should().Equal("Shangri-La", "San Agustin Church", "Fort Santiago");
        saved.Select(v => v.SortOrder).Should().Equal(0, 1, 2);
        saved[0].Id.Should().Be(venues[2].Id);
        saved[1].Id.Should().Be(venues[0].Id);
        (await Context.EventVenues.AnyAsync(v => v.Id == venues[1].Id)).Should().BeFalse();
        result.Select(v => v.Id).Should().Equal(saved.Select(v => v.Id));
    }

    [Fact]
    public async Task ShouldMoveADeletedVenuesItemsToTheMainVenue()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Casa Roces", "Shangri-La");
        var atCasaRoces = await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[1].Id, "Rehearsal");
        var atHotel = await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[2].Id, "Reception");
        var livestream = await EventProgramItemFactory.CreateAsync(@event.Id, "Livestream");

        // Casa Roces deleted
        await EventVenuesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildVenue("Manila Cathedral", venues[0].Id), BuildVenue("Shangri-La", venues[2].Id)));

        var items = await Context.EventProgramItems.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.VenueId);
        items[atCasaRoces[0].Id].Should().Be(venues[0].Id);
        items[atHotel[0].Id].Should().Be(venues[2].Id);
        items[livestream[0].Id].Should().BeNull();
    }

    [Fact]
    public async Task ShouldMoveTheItemsToTheNewMainVenue_WhenTheMainVenueIsDeleted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        var atCathedral = await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[0].Id, "Ceremony");
        var atHotel = await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[1].Id, "Reception");

        // The cathedral deleted; a new venue is the main one
        var result = await EventVenuesService.UpdateAsync(@event.Id, owner.Id,
            BuildRequest(BuildVenue("San Agustin Church"), BuildVenue("Shangri-La", venues[1].Id)));

        var newMainId = result[0].Id;
        newMainId.Should().NotBe(venues[0].Id).And.NotBe(venues[1].Id);
        var items = await Context.EventProgramItems.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.VenueId);
        items[atCathedral[0].Id].Should().Be(newMainId);
        items[atHotel[0].Id].Should().Be(venues[1].Id);
    }

    [Fact]
    public async Task ShouldLeaveTheItemsWithoutAVenue_WhenNoVenuesRemain()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[0].Id, "Ceremony");
        await EventProgramItemFactory.CreateAtVenueAsync(@event.Id, venues[1].Id, "Reception");

        var result = await EventVenuesService.UpdateAsync(@event.Id, owner.Id, BuildRequest());

        result.Should().BeEmpty();
        (await Context.EventVenues.CountAsync(v => v.EventId == @event.Id)).Should().Be(0);
        var items = await Context.EventProgramItems.AsNoTracking().Where(i => i.EventId == @event.Id).ToListAsync();
        items.Should().HaveCount(2).And.OnlyContain(i => i.VenueId == null);
    }

    [Fact]
    public async Task ShouldNotMoveAnotherEventsItems_WhenAVenueIsDeleted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La");
        var other = await EventFactory.CreateAsync(owner.Id);
        var otherVenues = await EventVenueFactory.CreateAsync(other.Id, "Elsewhere");
        var othersItems = await EventProgramItemFactory.CreateAtVenueAsync(other.Id, otherVenues[0].Id, "Party");

        await EventVenuesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(BuildVenue("Manila Cathedral", venues[0].Id)));

        (await Context.EventProgramItems.AsNoTracking().SingleAsync(i => i.Id == othersItems[0].Id)).VenueId.Should().Be(otherVenues[0].Id);
        (await Context.EventVenues.CountAsync(v => v.EventId == other.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData("https://www.google.com/maps/place/Manila+Cathedral/@14.59,120.97,17z", null)]
    [InlineData("https://maps.google.com/?q=14.59,120.97", null)]
    [InlineData("https://google.com/maps?q=Manila", null)]
    [InlineData("https://maps.app.goo.gl/AbCdEf123", null)]
    [InlineData("https://goo.gl/maps/AbCdEf123", null)]
    [InlineData(null, "https://maps.apple.com/?address=Manila&ll=14.59,120.97")]
    [InlineData(null, "https://maps.apple/p/AbCdEf")]
    [InlineData(null, "https://apple.co/4abcdef")]
    [InlineData(null, "https://beta.maps.apple.com/?ll=14.59,120.97")]
    public async Task ShouldSaveMapLinks_WhenTheyAreFromGoogleOrAppleMaps(string? googleMapsUrl, string? appleMapsUrl)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venue = BuildVenue();
        venue.GoogleMapsUrl = googleMapsUrl;
        venue.AppleMapsUrl = appleMapsUrl;

        var result = await EventVenuesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(venue));

        result.Single().Should().BeEquivalentTo(new { GoogleMapsUrl = googleMapsUrl, AppleMapsUrl = appleMapsUrl });
    }

    [Fact]
    public async Task ShouldThrowValidationErrorsKeyedByVenue_WhenTextIsMissingOrTooLong()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var noName = new UpdateEventVenueItemDto { Name = "   " };
        var tooLong = new UpdateEventVenueItemDto
        {
            Name = new string('a', 201),
            Address = new string('a', 301),
            Notes = new string('a', 1001),
            GoogleMapsUrl = $"https://www.google.com/maps/{new string('a', 480)}",
            AppleMapsUrl = $"https://maps.apple.com/?q={new string('a', 480)}",
            PhotoUrl = $"https://cdn.crephore.com/{new string('a', 480)}.jpg",
        };

        var errors = await SaveInvalidAsync(@event.Id, owner.Id, BuildVenue(), noName, tooLong);

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues[1].name"] = ["Name is required."],
            ["venues[2].name"] = ["Name must be 200 characters or fewer."],
            ["venues[2].address"] = ["Address must be 300 characters or fewer."],
            ["venues[2].notes"] = ["Notes must be 1000 characters or fewer."],
            ["venues[2].googleMapsUrl"] = ["Google Maps link must be 500 characters or fewer."],
            ["venues[2].appleMapsUrl"] = ["Apple Maps link must be 500 characters or fewer."],
            ["venues[2].photoUrl"] = ["Photo must be 500 characters or fewer."],
        });
        (await Context.EventVenues.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("http://www.google.com/maps/place/Manila")]
    [InlineData("https://www.bing.com/maps?q=Manila")]
    [InlineData("https://google.com.evil.example/maps")]
    [InlineData("https://notgoogle.com/maps")]
    [InlineData("www.google.com/maps")]
    [InlineData("javascript:alert(1)")]
    public async Task ShouldThrowValidationError_WhenTheGoogleMapsLinkIsNotFromGoogleMaps(string url)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venue = BuildVenue();
        venue.GoogleMapsUrl = url;

        var errors = await SaveInvalidAsync(@event.Id, owner.Id, venue);

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues[0].googleMapsUrl"] = ["Google Maps link must be a link from Google Maps."],
        });
    }

    [Theory]
    [InlineData("http://maps.apple.com/?ll=14.59,120.97")]
    [InlineData("https://www.apple.com/maps/")]
    [InlineData("https://maps.google.com/?q=Manila")]
    [InlineData("https://notapple.co/abc")]
    public async Task ShouldThrowValidationError_WhenTheAppleMapsLinkIsNotFromAppleMaps(string url)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venue = BuildVenue();
        venue.AppleMapsUrl = url;

        var errors = await SaveInvalidAsync(@event.Id, owner.Id, venue);

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues[0].appleMapsUrl"] = ["Apple Maps link must be a link from Apple Maps."],
        });
    }

    [Theory]
    [InlineData("hotel.jpg")]
    [InlineData("/uploads/events/1/hotel.jpg")]
    [InlineData("ftp://example.com/hotel.jpg")]
    public async Task ShouldThrowValidationError_WhenThePhotoIsNotALink(string url)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venue = BuildVenue();
        venue.PhotoUrl = url;

        var errors = await SaveInvalidAsync(@event.Id, owner.Id, venue);

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues[0].photoUrl"] = ["Photo must be a link."],
        });
    }

    [Fact]
    public async Task ShouldThrowValidationErrors_WhenThePinIsOutOfRangeOrIncomplete()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var outOfRange = BuildVenue();
        outOfRange.Latitude = 90.5m;
        outOfRange.Longitude = -180.1m;
        var onlyLatitude = BuildVenue();
        onlyLatitude.Latitude = 14.59m;
        var onlyLongitude = BuildVenue();
        onlyLongitude.Longitude = 120.97m;

        var errors = await SaveInvalidAsync(@event.Id, owner.Id, outOfRange, onlyLatitude, onlyLongitude);

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues[0].latitude"] = ["Latitude is not a valid latitude."],
            ["venues[0].longitude"] = ["Longitude is not a valid longitude."],
            ["venues[1].latitude"] = ["Latitude and longitude go together."],
            ["venues[2].latitude"] = ["Latitude and longitude go together."],
        });
    }

    [Fact]
    public async Task ShouldSaveThePin_WhenItIsAtTheLimits()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venue = BuildVenue();
        venue.Latitude = -90m;
        venue.Longitude = 180m;

        var result = await EventVenuesService.UpdateAsync(@event.Id, owner.Id, BuildRequest(venue));

        result.Single().Should().BeEquivalentTo(new { Latitude = (decimal?)-90m, Longitude = (decimal?)180m });
    }

    [Fact]
    public async Task ShouldThrowValidationError_WhenAVenueIsNotOneOfThisEvents()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id);
        var other = await EventFactory.CreateAsync(owner.Id);
        var otherVenues = await EventVenueFactory.CreateAsync(other.Id);

        var errors = await SaveInvalidAsync(@event.Id, owner.Id, BuildVenue(id: venues[0].Id), BuildVenue(id: otherVenues[0].Id));

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues"] = ["Venue 2 is no longer one of the event's venues. Reload the page to see the latest version."],
        });
        (await Context.EventVenues.AsNoTracking().SingleAsync(v => v.Id == otherVenues[0].Id)).EventId.Should().Be(other.Id);
    }

    [Fact]
    public async Task ShouldThrowValidationError_WhenThereAreMoreThanTenVenues()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var errors = await SaveInvalidAsync(@event.Id, owner.Id,
            [.. Enumerable.Range(1, 11).Select(n => BuildVenue($"Venue {n}"))]);

        errors.Should().BeEquivalentTo(new Dictionary<string, List<string>>
        {
            ["venues"] = ["An event can have at most 10 venues."],
        });
        (await Context.EventVenues.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowForbidden_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        // Access to the event, but not Owner
        await EventUserFactory.CreateAsync(@event.Id, member.Id);

        var act = () => EventVenuesService.UpdateAsync(@event.Id, member.Id, BuildRequest(BuildVenue()));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ShouldThrowNotFound_WhenUserHasNoAccessToTheEvent()
    {
        var owner = await AppUserFactory.CreateAsync();
        var stranger = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = () => EventVenuesService.UpdateAsync(@event.Id, stranger.Id, BuildRequest(BuildVenue()));

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
