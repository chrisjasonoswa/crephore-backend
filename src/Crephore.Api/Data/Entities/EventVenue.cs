using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Data.Entities;

/*
 * A place where an event happens, e.g. the ceremony's church or the reception's hotel. An event has any number of them
 * in the host's order (SortOrder from 0); the first is its main venue, the one the website's hero, the intros and the
 * dashboard show. Program items point to one of them (EventProgramItem.VenueId). Deleted with the event.
 */
[Table("event_venues")]
public class EventVenue : IHasTimestamp
{
    public int Id { get; set; }

    // Position in the event's venues, from 0; the first is the main venue
    public int SortOrder { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;
    [MaxLength(300)]
    public string? Address { get; set; }

    // Links the host pastes from Google Maps / Apple Maps
    [MaxLength(500)]
    public string? GoogleMapsUrl { get; set; }
    [MaxLength(500)]
    public string? AppleMapsUrl { get; set; }

    // The pin, which the web app reads from the links above; both or neither
    [Precision(9, 6)]
    public decimal? Latitude { get; set; }
    [Precision(9, 6)]
    public decimal? Longitude { get; set; }

    // An uploaded photo's URL (POST api/events/{eventId}/media)
    [MaxLength(500)]
    public string? PhotoUrl { get; set; }
    // Free text for guests, e.g. directions and parking
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /* Related Properties */
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    // The program items held here (cleared when the venue is deleted)
    public ICollection<EventProgramItem> ProgramItems { get; set; } = new List<EventProgramItem>();
}
