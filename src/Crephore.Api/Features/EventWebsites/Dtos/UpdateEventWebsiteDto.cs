using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Crephore.Api.Common.Constants;

namespace Crephore.Api.Features.EventWebsites.Dtos;

// The whole website: its theme and every section in page order. Saving replaces what was there.
public class UpdateEventWebsiteRequestDto
{
    [Required]
    public WebsiteThemeDto Theme { get; set; } = null!;

    [Required]
    [MaxLength(EventWebsitesService.MaxSections, ErrorMessage = "The sections can be at most 50.")]
    public List<UpdateWebsiteSectionRequestDto> Sections { get; set; } = [];
}

public class UpdateWebsiteSectionRequestDto
{
    // A saved section of this website to update; null for a new section. Saved sections left out are deleted.
    public int? Id { get; set; }

    // Keys from WebsiteKeys, e.g. "hero" + "split" on "light"
    [Required]
    [MaxLength(32)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Layout { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Background { get; set; } = string.Empty;

    // The layout's placement choices, e.g. { "textPosition": "bottom-left" }
    public Dictionary<string, string> Options { get; set; } = [];

    // A JSON object with the section's text and photos; stored as it is (see WebsiteSection)
    public JsonElement Content { get; set; }

    [Range(1, int.MaxValue)]
    public int ContentVersion { get; set; } = 1;

    public bool IsVisible { get; set; } = true;

    // When guests see it; Always when left out
    [EnumDataType(typeof(SectionVisibility))]
    public SectionVisibility Visibility { get; set; } = SectionVisibility.Always;
}
