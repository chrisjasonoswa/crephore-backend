using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventGroups;
using Crephore.Api.Features.Events.Dtos;

namespace Crephore.Api.Features.Events;

public static class EventsMapper
{
    // The event as one user sees it: their own access and group.
    // Expects Event and EventGroup to be loaded; `venueName` is the main venue's name ("" without venues).
    public static EventDto ToEventDto(this EventUser eventUser, string venueName)
    {
        var @event = eventUser.Event;
        return new EventDto
        {
            Id = @event.Id,
            Name = @event.Name,
            Type = @event.Type,
            VenueName = venueName,
            StartsAt = @event.StartsAt,
            EndsAt = @event.EndsAt,
            Timezone = @event.Timezone,
            BornOn = @event.BornOn,
            DiedOn = @event.DiedOn,
            Subdomain = @event.Subdomain,
            CreatedAt = @event.CreatedAt,
            Access = [.. eventUser.Access],
            EventGroup = eventUser.EventGroup?.ToEventGroupSummaryDto(),
        };
    }
}
