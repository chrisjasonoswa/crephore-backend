using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.EventVenues.Dtos;

// Every venue of the event, in order (the first is the main venue): saving replaces what was there. The service checks
// them (at most 10, each keyed by its position, e.g. "venues[1].name"), so the web app can show errors beside each one.
public class UpdateEventVenuesRequestDto
{
    [Required]
    public List<UpdateEventVenueItemDto> Venues { get; set; } = [];
}

public class UpdateEventVenueItemDto
{
    // A saved venue of this event to update; null for a new one. Saved venues left out are deleted, and their program
    // items move to the main venue.
    public int? Id { get; set; }

    // Required; the rest are optional (left empty or blank, they're saved as null)
    public string? Name { get; set; }
    public string? Address { get; set; }
    // https links from Google Maps (google.com, goo.gl) and Apple Maps (maps.apple.com, maps.apple, apple.co)
    public string? GoogleMapsUrl { get; set; }
    public string? AppleMapsUrl { get; set; }
    // The pin the web app reads from those links: both or neither
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    // A URL returned by POST api/events/{eventId}/media
    public string? PhotoUrl { get; set; }
    // Free text for guests, e.g. directions and parking
    public string? Notes { get; set; }
}
