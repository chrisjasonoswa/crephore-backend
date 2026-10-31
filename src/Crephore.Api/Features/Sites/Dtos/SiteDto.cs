using Crephore.Api.Features.EventWebsites.Dtos;

namespace Crephore.Api.Features.Sites.Dtos;

// A published event website as guests see it. Only what the page needs: no ids, no hidden sections, no access.
public class SiteDto
{
    public string Subdomain { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    // The event's start (UTC) and timezone, live from the event (e.g. the countdown's target)
    public DateTimeOffset StartsAt { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }

    // As it was when last published
    public WebsiteThemeDto Theme { get; set; } = new();
    public List<PublishedSectionDto> Sections { get; set; } = [];
}
