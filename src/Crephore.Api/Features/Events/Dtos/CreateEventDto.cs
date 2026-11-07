using System.ComponentModel.DataAnnotations;
using Crephore.Api.Common.Constants;

namespace Crephore.Api.Features.Events.Dtos;

// The event's own fields and rules, shared by create and update
public class EventRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public EventType? Type { get; set; }

    // Wall-clock times at the venue without an offset (e.g. "2027-06-12T15:00"), converted to UTC with Timezone
    [Required]
    public DateTime? StartsAt { get; set; }

    // Optional: an event without a set end
    public DateTime? EndsAt { get; set; }

    // A memorial's dates of birth and passing ("1948-02-14"), each optional; ignored (saved as null) for other types.
    // The date of passing must not be before the date of birth (checked in EventsService).
    public DateOnly? BornOn { get; set; }
    public DateOnly? DiedOn { get; set; }

    // IANA time zone, e.g. "Asia/Manila"; checked in EventsService
    [Required]
    [MaxLength(64)]
    public string Timezone { get; set; } = string.Empty;

    // Shown to hosts as "Website address". Lowercase letters, numbers and hyphens (normalized to lowercase);
    // checked in EventsService.
    [Required(ErrorMessage = "The website address is required.")]
    [MaxLength(63, ErrorMessage = "The website address must be 63 characters or fewer.")]
    public string Subdomain { get; set; } = string.Empty;

}

public class CreateEventRequestDto : EventRequestDto
{
    // Optional group (must belong to the same user). Only on create: afterwards events are grouped through the groups
    // endpoints, and updating an event never changes its group.
    public int? EventGroupId { get; set; }
}
