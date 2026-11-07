namespace Crephore.Api.Features.EventPrograms.Dtos;

// An event's program (its schedule): its items in the host's order.
// Also what guests get with a published website (SiteDto), read live from the event.
public class EventProgramDto
{
    public List<EventProgramItemDto> Items { get; set; } = [];
}

// Dates and times as the web app shows them, in the event's timezone: "2027-02-14", "15:00"
public class EventProgramItemDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    // One of the event's venues (EventVenueDto.Id); null for none
    public int? VenueId { get; set; }
    public string? Speakers { get; set; }
}
