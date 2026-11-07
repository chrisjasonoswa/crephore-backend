using Crephore.Api.Common.Exceptions;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventVenues.Services;

public class GetEventVenuesTests(DatabaseFixture fixture)
    : EventVenuesBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldReturnTheVenuesInTheHostsOrder()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var venues = await EventVenueFactory.CreateAsync(@event.Id, "Manila Cathedral", "Shangri-La", "Casa Roces");
        // Reordered by the host since: Shangri-La is now the main venue
        venues[0].SortOrder = 1;
        venues[1].SortOrder = 0;
        venues[1].GoogleMapsUrl = "https://maps.app.goo.gl/abc";
        venues[1].Latitude = 14.581234m;
        venues[1].Longitude = 121.056789m;
        await Context.SaveChangesAsync();

        var result = await EventVenuesService.GetAsync(@event.Id, owner.Id);

        result.Select(v => v.Name).Should().Equal("Shangri-La", "Manila Cathedral", "Casa Roces");
        result[0].Should().BeEquivalentTo(new
        {
            Id = venues[1].Id,
            Name = "Shangri-La",
            Address = "2 Test Street, Manila",
            GoogleMapsUrl = "https://maps.app.goo.gl/abc",
            AppleMapsUrl = (string?)null,
            Latitude = (decimal?)14.581234m,
            Longitude = (decimal?)121.056789m,
            PhotoUrl = (string?)null,
            Notes = (string?)null,
        });
    }

    [Fact]
    public async Task ShouldReturnAnEmptyList_WhenTheEventHasNoVenues()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var result = await EventVenuesService.GetAsync(@event.Id, owner.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldReturnTheVenues_WhenUserHasAccessButIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);
        await EventVenueFactory.CreateAsync(@event.Id);

        var result = await EventVenuesService.GetAsync(@event.Id, member.Id);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task ShouldThrowNotFound_WhenUserHasNoAccessToTheEvent()
    {
        var owner = await AppUserFactory.CreateAsync();
        var stranger = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = () => EventVenuesService.GetAsync(@event.Id, stranger.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
