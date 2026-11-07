using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.EventPrograms.Dtos;

// The whole program: saving replaces what was there. Each item is checked by the service, so its errors are keyed by
// its position (e.g. "items[2].title") for the web app to show next to that item.
public class UpdateEventProgramRequestDto
{
    [Required]
    [MaxLength(EventProgramsService.MaxItems, ErrorMessage = "The program can have at most 40 items.")]
    public List<UpdateEventProgramItemRequestDto> Items { get; set; } = [];
}

public class UpdateEventProgramItemRequestDto
{
    // A saved item of this event to update; null for a new item. Saved items left out are deleted.
    public int? Id { get; set; }

    // "2027-02-14" (required), "15:00" (optional), in the event's timezone; an end earlier than the start means it
    // ends after midnight
    public string? Date { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }

    // Required; the rest are optional (left empty or blank, they're saved as null)
    public string? Title { get; set; }
    public string? Description { get; set; }
    // One of the event's venues (EventVenueDto.Id); null for none (e.g. a livestream)
    public int? VenueId { get; set; }
    public string? Speakers { get; set; }
}
