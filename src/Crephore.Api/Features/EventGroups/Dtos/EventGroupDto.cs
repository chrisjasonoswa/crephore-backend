using Crephore.Api.Common.Constants;

namespace Crephore.Api.Features.EventGroups.Dtos;

public class EventGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Ordered by start date
    public List<EventGroupEventDto> Events { get; set; } = [];
}

public class EventGroupEventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EventType Type { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string Timezone { get; set; } = string.Empty;
}
