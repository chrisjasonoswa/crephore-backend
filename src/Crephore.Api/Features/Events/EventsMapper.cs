using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventGroups;
using Crephore.Api.Features.Events.Dtos;

namespace Crephore.Api.Features.Events;

public static class EventsMapper
{
    // The event as one user sees it: their own access and group.
    // Expects Event and EventGroup to be loaded.
    public static EventDto ToEventDto(this EventUser eventUser)
    {
        var @event = eventUser.Event;
        return new EventDto
        {
            Id = @event.Id,
            Name = @event.Name,
            Description = @event.Description,
            Type = @event.Type,
            VenueName = @event.VenueName,
            VenueAddress = @event.VenueAddress,
            VenueLatitude = @event.VenueLatitude,
            VenueLongitude = @event.VenueLongitude,
            VenueGooglePlaceId = @event.VenueGooglePlaceId,
            StartsAt = @event.StartsAt,
            EndsAt = @event.EndsAt,
            Timezone = @event.Timezone,
            Subdomain = @event.Subdomain,
            CreatedAt = @event.CreatedAt,
            Access = [.. eventUser.Access],
            EventGroup = eventUser.EventGroup?.ToEventGroupSummaryDto(),
            Timeline = [.. @event.Timeline],
        };
    }
}
