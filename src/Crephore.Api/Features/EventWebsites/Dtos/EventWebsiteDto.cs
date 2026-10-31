using System.ComponentModel.DataAnnotations;
using Crephore.Api.Common.Constants;
using System.Text.Json;

namespace Crephore.Api.Features.EventWebsites.Dtos;

public class EventWebsiteDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public WebsiteThemeDto Theme { get; set; } = new();
    // In page order
    public List<WebsiteSectionDto> Sections { get; set; } = [];
    // When it was last published; null while guests can't see it
    public DateTimeOffset? PublishedAt { get; set; }
    // Published, but guests see an older version than the saved one
    public bool HasUnpublishedChanges { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// The website's look; the same shape as the web app's SiteTheme. Also the theme of a save request.
public class WebsiteThemeDto
{
    private const string HexPattern = "^#[0-9A-Fa-f]{6}$";
    private const string HexMessage = "The {0} must be a colour like #1E2A44.";

    [Required]
    [RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string PrimaryColor { get; set; } = string.Empty;

    [Required]
    [RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string AccentColor { get; set; } = string.Empty;

    // The light page colour; null = plain white
    [RegularExpression(HexPattern, ErrorMessage = HexMessage)]
    public string? BackgroundColor { get; set; }

    // A key from WebsiteKeys.FontPairings, e.g. "serif_classic"
    [Required]
    [MaxLength(32)]
    public string FontPairingKey { get; set; } = string.Empty;

    // "sharp", "soft" or "round"
    [Required]
    [MaxLength(16)]
    public string CornerStyle { get; set; } = string.Empty;
}

public class WebsiteSectionDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = [];
    // The section's text and photos, as the web app saved them
    public JsonElement Content { get; set; }
    public int ContentVersion { get; set; }
    public bool IsVisible { get; set; }
    public SectionVisibility Visibility { get; set; }
}
