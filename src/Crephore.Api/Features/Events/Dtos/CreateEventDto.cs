using System.ComponentModel.DataAnnotations;
using Crephore.Api.Common.Constants;

namespace Crephore.Api.Features.Events.Dtos;

public class CreateEventRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public EventType? Type { get; set; }

    [Required]
    [MaxLength(200)]
    public string VenueName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string VenueAddress { get; set; } = string.Empty;

    // Filled once Google Places is integrated; send null for now.
    [Range(-90, 90)]
    public decimal? VenueLatitude { get; set; }

    [Range(-180, 180)]
    public decimal? VenueLongitude { get; set; }

    [MaxLength(255)]
    public string? VenueGooglePlaceId { get; set; }

    // Wall-clock times at the venue without an offset (e.g. "2027-06-12T15:00"), converted to UTC with Timezone
    [Required]
    public DateTime? StartsAt { get; set; }

    [Required]
    public DateTime? EndsAt { get; set; }

    // IANA time zone, e.g. "Asia/Manila"; checked in EventsService
    [Required]
    [MaxLength(64)]
    public string Timezone { get; set; } = string.Empty;

    // Shown to hosts as "Website address". Lowercase letters, numbers and hyphens (normalized to lowercase);
    // checked in EventsService.
    [Required(ErrorMessage = "The website address is required.")]
    [MaxLength(63, ErrorMessage = "The website address must be 63 characters or fewer.")]
    public string Subdomain { get; set; } = string.Empty;

    // Optional group (must belong to the same user)
    public int? EventGroupId { get; set; }

    // The event's program in order, e.g. ["Opening", "Dinner"]; items are checked in EventsService
    public List<string> Timeline { get; set; } = [];
}
