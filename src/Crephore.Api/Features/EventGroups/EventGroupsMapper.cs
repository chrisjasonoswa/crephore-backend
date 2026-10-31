using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventGroups.Dtos;

namespace Crephore.Api.Features.EventGroups;

public static class EventGroupsMapper
{
    // Expects EventUsers (with Event) to be loaded
    public static EventGroupDto ToEventGroupDto(this EventGroup group)
    {
        return new EventGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            EventCount = group.EventUsers.Count,
            CreatedAt = group.CreatedAt,
            Events = [.. group.EventUsers
                .Select(eu => eu.Event)
                .OrderBy(e => e.StartsAt)
                .ThenBy(e => e.Id)
                .Select(e => new EventGroupEventDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Type = e.Type,
                    StartsAt = e.StartsAt,
                    Timezone = e.Timezone,
                })],
        };
    }

    public static EventGroupSummaryDto ToEventGroupSummaryDto(this EventGroup group)
    {
        return new EventGroupSummaryDto
        {
            Id = group.Id,
            Name = group.Name,
        };
    }
}
