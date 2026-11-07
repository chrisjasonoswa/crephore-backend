using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

/*
 * One part of an event's program (its schedule), e.g. "3:00 PM · Ceremony". The program belongs to the event, so every
 * program section of its website shows the same items. Date and times are wall-clock values in the event's Timezone,
 * like the event's own times when entered; an end time earlier than the start means it ends after midnight.
 */
[Table("event_program_items")]
public class EventProgramItem : IHasTimestamp
{
    public int Id { get; set; }

    // The item's calendar day, e.g. 2027-02-14; times are optional (an untimed item shows a number or nothing)
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    [MaxLength(150)]
    public string Title { get; set; } = null!;
    // A note under the item, e.g. "Please be seated by 2:45."
    [MaxLength(500)]
    public string? Description { get; set; }
    // Comma-separated, e.g. "Ana Lim, Paolo Tan"
    [MaxLength(300)]
    public string? Speakers { get; set; }

    // Position in the program, from 0; items are shown in the host's order, never sorted
    public int SortOrder { get; set; }

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /* Related Properties */
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    // Where it happens: one of the event's venues; null for none (e.g. a livestream), and when the venue is deleted
    // without another left to move to
    public int? VenueId { get; set; }
    public EventVenue? Venue { get; set; }
}
