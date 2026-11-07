using Crephore.Api.Common.Constants;
using Crephore.Api.Features.EventPrograms.Dtos;
using Crephore.Api.Features.EventVenues.Dtos;
using Crephore.Api.Features.EventWebsites.Dtos;

namespace Crephore.Api.Features.Sites.Dtos;

// A published event website as guests see it. Only what the page needs: no ids, no hidden sections, no access.
public class SiteDto
{
    public string Subdomain { get; set; } = string.Empty;
    // The event's name and its main venue's name ("" without venues), live from the event (the hero's and countdown's
    // heading, the hero's place)
    public string EventName { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    // The kind of event, e.g. Wedding (what the intros fall back to without the host's own content)
    public EventType EventType { get; set; }
    // A memorial's dates of birth and passing (the hero and the intros show them); null for other types
    public DateOnly? BornOn { get; set; }
    public DateOnly? DiedOn { get; set; }
    // The event's start (UTC) and timezone, live from the event (e.g. the countdown's target)
    public DateTimeOffset StartsAt { get; set; }
    public string Timezone { get; set; } = string.Empty;
    // The event's program, live from the event (the program sections show it)
    public EventProgramDto Program { get; set; } = new();
    // The event's venues in order (the first is the main venue), live from the event; program items point to them
    public List<EventVenueDto> Venues { get; set; } = [];
    public DateTimeOffset PublishedAt { get; set; }

    // As it was when last published
    public WebsiteThemeDto Theme { get; set; } = new();
    public List<PublishedSectionDto> Sections { get; set; } = [];
}
