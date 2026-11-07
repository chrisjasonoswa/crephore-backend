using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Common.Constants;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

/*
 * The actual event, e.g. a birthday. Users reach it through EventUsers (who can see it and what they can do);
 * every query goes through the current user's EventUser row.
 */
[Table("events")]
public class Event : IHasCreatedAt
{
    public int Id { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = null!;
    public EventType Type { get; set; }

    // A memorial's person: their dates of birth and passing (the hero and the intros show them), each optional. Only
    // kept for memorials; saving another type clears them.
    public DateOnly? BornOn { get; set; }
    public DateOnly? DiedOn { get; set; }

    // Stored in UTC; shown in Timezone (with the timezone named) everywhere, including the event website. EndsAt is
    // optional: null for an event without a set end.
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    // The venue's IANA time zone, e.g. "Asia/Manila". The event's times are entered and shown in it.
    [MaxLength(64)]
    public string Timezone { get; set; } = null!;

    // The event website's subdomain, e.g. "birthday-ni-cj-2021" → birthday-ni-cj-2021.crephore.com.
    // Stored lowercase; unique across all events.
    [MaxLength(63)]
    public string Subdomain { get; set; } = null!;

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }

    /* Related Properties */
    public ICollection<EventUser> EventUsers { get; set; } = new List<EventUser>();
    // Null until the host saves a website
    public EventWebsite? Website { get; set; }
    public ICollection<Media> Media { get; set; } = new List<Media>();
    // The program (schedule), in SortOrder
    public ICollection<EventProgramItem> ProgramItems { get; set; } = new List<EventProgramItem>();
    // Where it happens, in SortOrder; the first is the main venue (none until the host adds one)
    public ICollection<EventVenue> Venues { get; set; } = new List<EventVenue>();
}
