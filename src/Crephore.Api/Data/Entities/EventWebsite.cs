using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;
using Crephore.Api.Features.EventWebsites;

namespace Crephore.Api.Data.Entities;

/*
 * An event's website: its theme and its sections (WebsiteSection, in page order). At most one per event.
 * Saving changes the website; publishing copies it into PublishedContent, which is what guests see.
 * The subdomain stays on Event. Theme keys (FontPairingKey, CornerStyle, the Motion* keys) are the web app's keys
 * (see WebsiteKeys); colours are "#RRGGBB".
 */
[Table("event_websites")]
public class EventWebsite : IHasTimestamp
{
    public int Id { get; set; }

    // Theme
    [MaxLength(7)]
    public string PrimaryColor { get; set; } = null!;
    // Optional: the second brand colour (e.g. section backgrounds) and a small highlight colour (buttons, icons)
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    [MaxLength(7)]
    public string? AccentColor { get; set; }
    // The light page colour; null = plain white
    [MaxLength(7)]
    public string? BackgroundColor { get; set; }
    [MaxLength(32)]
    public string FontPairingKey { get; set; } = null!;
    [MaxLength(16)]
    public string CornerStyle { get; set; } = null!;
    // How big the hero's heading and the other sections' headings are set (WebsiteKeys.HeadingSizes). Websites saved
    // before these existed are medium (database defaults, see AppDbContext). The web app no longer uses HeroHeadingSize:
    // the hero (shown to hosts as the "Cover") has its own "Heading size" option in its section options, a saved value
    // moves there when the website loads, and saving always sends medium.
    [MaxLength(16)]
    public string HeroHeadingSize { get; set; } = WebsiteKeys.DefaultHeadingSize;
    [MaxLength(16)]
    public string HeadingSize { get; set; } = WebsiteKeys.DefaultHeadingSize;

    // Motion: the intro when the site opens, and how strongly (the intensity scales the intro's speed). Websites saved before motion existed have the defaults (database defaults, see AppDbContext).
    [MaxLength(32)]
    public string MotionIntro { get; set; } = WebsiteKeys.DefaultPageIntro;
    [MaxLength(16)]
    public string MotionIntensity { get; set; } = WebsiteKeys.DefaultMotionIntensity;

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
