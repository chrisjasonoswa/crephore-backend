namespace Crephore.Api.Features.EventVenues.Dtos;

// One of an event's venues. Lists of them are in the host's order; the first is the main venue. Optional fields are
// null when unset. Also what guests get with a published website (SiteDto), read live from the event.
public class EventVenueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public string? AppleMapsUrl { get; set; }
    // The pin: both or neither
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PhotoUrl { get; set; }
    // Free text for guests, e.g. directions and parking
    public string? Notes { get; set; }
}
