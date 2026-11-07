using Crephore.Api.Common.Constants;
using Crephore.Api.Features.EventGroups.Dtos;

namespace Crephore.Api.Features.Events.Dtos;

public class EventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EventType Type { get; set; }
    // The main venue's name (the first of its venues, see api/events/{id}/venues); "" for an event without venues.
    // Read-only: venues are saved through the venues endpoints.
    public string VenueName { get; set; } = string.Empty;
    // UTC; show them in Timezone. EndsAt is null for an event without a set end.
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string Timezone { get; set; } = string.Empty;
    // A memorial's dates of birth and passing; null for other types
    public DateOnly? BornOn { get; set; }
    public DateOnly? DiedOn { get; set; }
    public string Subdomain { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // The current user's access, e.g. ["Owner"]
    public List<EventAccess> Access { get; set; } = [];

    // The current user's own group; null when they have not grouped the event
    public EventGroupSummaryDto? EventGroup { get; set; }
}
