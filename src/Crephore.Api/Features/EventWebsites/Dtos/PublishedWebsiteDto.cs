using System.Text.Json;
using Crephore.Api.Common.Constants;

namespace Crephore.Api.Features.EventWebsites.Dtos;

// The website as guests see it: stored in EventWebsite.PublishedContent when publishing, and served by the public
// site endpoint. Only the visible sections, in page order, without the editor's ids.
public class PublishedWebsiteDto
{
    public WebsiteThemeDto Theme { get; set; } = new();
    public List<PublishedSectionDto> Sections { get; set; } = [];
}

public class PublishedSectionDto
{
    public string Type { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = [];
    public JsonElement Content { get; set; }
    public int ContentVersion { get; set; }
    public SectionVisibility Visibility { get; set; }
}
