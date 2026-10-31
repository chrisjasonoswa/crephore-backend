using Crephore.Api.Common.Constants;
using Crephore.Api.Features.EventGroups.Dtos;

namespace Crephore.Api.Features.Events.Dtos;

public class EventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EventType Type { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueAddress { get; set; } = string.Empty;
    public decimal? VenueLatitude { get; set; }
    public decimal? VenueLongitude { get; set; }
    public string? VenueGooglePlaceId { get; set; }
    // UTC; show them (and the timeline's) in Timezone
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // The current user's access, e.g. ["Owner"]
    public List<EventAccess> Access { get; set; } = [];

    // The current user's own group; null when they have not grouped the event
    public EventGroupSummaryDto? EventGroup { get; set; }

    // The program in order, e.g. ["Opening", "Dinner"]
    public List<string> Timeline { get; set; } = [];
}
