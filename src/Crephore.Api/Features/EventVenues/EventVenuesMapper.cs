using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventVenues.Dtos;
using Crephore.Api.Features.Media.Dtos;

namespace Crephore.Api.Features.EventVenues;

public static class EventVenuesMapper
{
    // The venues in the host's order (the first is the main venue). `mediaUrls` (by media id) holds their photos' URLs
    // (IMediaService.GetUrlsAsync with PhotoMediaIds).
    public static List<EventVenueDto> ToEventVenueDtos(IEnumerable<EventVenue> venues, IReadOnlyDictionary<int, string> mediaUrls) =>
        venues.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => v.ToEventVenueDto(mediaUrls)).ToList();

    // The media ids of the venues' photos
    public static IEnumerable<int> PhotoMediaIds(IEnumerable<EventVenue> venues) =>
        venues.Where(v => v.PhotoMediaId is not null).Select(v => v.PhotoMediaId!.Value);

    // The main venue's name; "" for an event without venues
    public static string MainVenueName(IEnumerable<EventVenue> venues) =>
        venues.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => v.Name).FirstOrDefault() ?? string.Empty;

    public static EventVenueDto ToEventVenueDto(this EventVenue venue, IReadOnlyDictionary<int, string> mediaUrls) => new()
    {
        Id = venue.Id,
        Name = venue.Name,
        Address = venue.Address,
        GoogleMapsUrl = venue.GoogleMapsUrl,
        AppleMapsUrl = venue.AppleMapsUrl,
        Latitude = venue.Latitude,
        Longitude = venue.Longitude,
        Photo = venue.PhotoMediaId is { } id && mediaUrls.TryGetValue(id, out var url)
            ? new MediaReferenceDto { MediaId = id, Url = url }
            : null,
        Notes = venue.Notes,
    };
}
