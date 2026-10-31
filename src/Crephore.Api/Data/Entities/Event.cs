using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Common.Constants;
using Crephore.Api.Data.IEntities;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Data.Entities;

/*
 * The actual event, e.g. a birthday. Users reach it through EventUsers (who can see it and what they can do);
 * every query goes through the current user's EventUser row. Its program lives in Timeline.
 */
[Table("events")]
public class Event : IHasCreatedAt
{
    public int Id { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = null!;
    [MaxLength(2000)]
    public string? Description { get; set; }
    public EventType Type { get; set; }

    // Venue. Latitude, longitude and Google place id are filled once Google Places is integrated.
    [MaxLength(200)]
    public string VenueName { get; set; } = null!;
    [MaxLength(500)]
    public string VenueAddress { get; set; } = null!;
    [Precision(9, 6)]
    public decimal? VenueLatitude { get; set; }
    [Precision(9, 6)]
    public decimal? VenueLongitude { get; set; }
    [MaxLength(255)]
    public string? VenueGooglePlaceId { get; set; }

    // Stored in UTC; shown in Timezone (with the timezone named) everywhere, including the event website
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    // The venue's IANA time zone, e.g. "Asia/Manila". The event's and its timeline's times are entered and shown in it.
    [MaxLength(64)]
    public string Timezone { get; set; } = null!;

    // The event website's subdomain, e.g. "birthday-ni-cj-2021" → birthday-ni-cj-2021.crephore.com.
    // Stored lowercase; unique across all events.
    [MaxLength(63)]
    public string Subdomain { get; set; } = null!;

    // Timestamp
    // The program in order, e.g. ["Opening", "Dinner", "First dance"]; a JSON array of strings
    public List<string> Timeline { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    /* Related Properties */
    public ICollection<EventUser> EventUsers { get; set; } = new List<EventUser>();
    // Null until the host saves a website
    public EventWebsite? Website { get; set; }
    public ICollection<Media> Media { get; set; } = new List<Media>();
}
