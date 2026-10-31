using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

/*
 * An event's website: its theme and its sections (WebsiteSection, in page order). At most one per event.
 * Saving changes the website; publishing copies it into PublishedContent, which is what guests see.
 * The subdomain stays on Event. Theme keys (FontPairingKey, CornerStyle) are the web app's keys
 * (see WebsiteKeys); colours are "#RRGGBB".
 */
[Table("event_websites")]
public class EventWebsite : IHasTimestamp
{
    public int Id { get; set; }

    // Theme
    [MaxLength(7)]
    public string PrimaryColor { get; set; } = null!;
    [MaxLength(7)]
    public string AccentColor { get; set; } = null!;
    // The light page colour; null = plain white
    [MaxLength(7)]
    public string? BackgroundColor { get; set; }
    [MaxLength(32)]
    public string FontPairingKey { get; set; } = null!;
    [MaxLength(16)]
    public string CornerStyle { get; set; } = null!;

    // Publishing: guests see the website as it was when last published (PublishedWebsiteSnapshot as JSON: the theme
    // and the visible sections), not the latest save. Both null while unpublished.
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedContent { get; set; }

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /* Related Properties */
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public ICollection<WebsiteSection> Sections { get; set; } = new List<WebsiteSection>();
}
