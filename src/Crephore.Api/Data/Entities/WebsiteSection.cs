using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Common.Constants;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

/*
 * One section of an event's website, e.g. its hero. Section types, layouts, their options and content fields
 * are defined in the web app (section registry), not here: the API checks the keys (WebsiteKeys) and stores
 * Options and Content as JSON without knowing their fields. Keys are stored, so they are never renamed.
 */
[Table("website_sections")]
public class WebsiteSection : IHasTimestamp
{
    public int Id { get; set; }

    // Section type and layout keys, e.g. "hero" + "split"
    [MaxLength(32)]
    public string Type { get; set; } = null!;
    [MaxLength(32)]
    public string Layout { get; set; } = null!;
    // "light", "photo", "primary" or "accent"
    [MaxLength(16)]
    public string Background { get; set; } = null!;

    // The layout's placement choices, e.g. { "textPosition": "bottom-left" }; missing keys use the layout's defaults
    public Dictionary<string, string> Options { get; set; } = [];

    // The section's text and photos as a JSON object, shaped by its type in the web app
    public string Content { get; set; } = "{}";
    // Lets the web app upgrade old content when a type's fields change
    public int ContentVersion { get; set; } = 1;

    // Hidden sections stay in the editor but are not shown to guests
    public bool IsVisible { get; set; } = true;
    // When guests see it (around the event's start); Always for now
    public SectionVisibility Visibility { get; set; } = SectionVisibility.Always;
    // Position on the page, from 0
    public int SortOrder { get; set; }

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /* Related Properties */
    public int EventWebsiteId { get; set; }
    public EventWebsite EventWebsite { get; set; } = null!;
}
