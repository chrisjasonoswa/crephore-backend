using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventVenues.Dtos;

namespace Crephore.Api.Features.EventVenues;

public static class EventVenuesMapper
{
    // The venues in the host's order (the first is the main venue)
    public static List<EventVenueDto> ToEventVenueDtos(IEnumerable<EventVenue> venues) =>
        venues.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(ToEventVenueDto).ToList();

    // The main venue's name; "" for an event without venues
    public static string MainVenueName(IEnumerable<EventVenue> venues) =>
        venues.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => v.Name).FirstOrDefault() ?? string.Empty;

    public static EventVenueDto ToEventVenueDto(this EventVenue venue) => new()
    {
        Id = venue.Id,
        Name = venue.Name,
        Address = venue.Address,
        GoogleMapsUrl = venue.GoogleMapsUrl,
        AppleMapsUrl = venue.AppleMapsUrl,
        Latitude = venue.Latitude,
        Longitude = venue.Longitude,
        PhotoUrl = venue.PhotoUrl,
        Notes = venue.Notes,
    };
}
