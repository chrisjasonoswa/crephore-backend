using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventWebsites.Dtos;

namespace Crephore.Api.Features.EventWebsites;

public static class EventWebsitesMapper
{
    // Expects Sections to be loaded
    public static EventWebsiteDto ToEventWebsiteDto(this EventWebsite website)
    {
        return new EventWebsiteDto
        {
            Id = website.Id,
            EventId = website.EventId,
            Theme = website.ToWebsiteThemeDto(),
            Sections = [.. website.Sections
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Id)
                .Select(s => s.ToWebsiteSectionDto())],
            PublishedAt = website.PublishedAt,
            HasUnpublishedChanges = website.HasUnpublishedChanges(),
            CreatedAt = website.CreatedAt,
            UpdatedAt = website.UpdatedAt,
        };
    }

    // What publishing stores: the theme and the visible sections in page order. Expects Sections to be loaded.
    public static PublishedWebsiteDto ToPublishedWebsiteDto(this EventWebsite website)
    {
        return new PublishedWebsiteDto
        {
            Theme = website.ToWebsiteThemeDto(),
            Sections = [.. website.Sections
                .Where(s => s.IsVisible)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Id)
                .Select(s => new PublishedSectionDto
                {
                    Type = s.Type,
                    Layout = s.Layout,
                    Background = s.Background,
                    Options = new Dictionary<string, string>(s.Options),
                    Content = JsonSerializer.Deserialize<JsonElement>(s.Content),
                    ContentVersion = s.ContentVersion,
                    Visibility = s.Visibility,
                })],
        };
    }

    // Stored like the API's JSON: camelCase, enums as names
    private static readonly JsonSerializerOptions PublishedContentJson = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToPublishedContent(this EventWebsite website) =>
        JsonSerializer.Serialize(website.ToPublishedWebsiteDto(), PublishedContentJson);

    public static PublishedWebsiteDto? ReadPublishedContent(string? publishedContent) =>
        publishedContent is null ? null : JsonSerializer.Deserialize<PublishedWebsiteDto>(publishedContent, PublishedContentJson);

    // Whether guests see something other than what publishing now would store. Compared as JSON values, since MySQL
    // reorders the keys of stored JSON. Hidden sections don't count: guests don't see them either way.
    // The stored copy is read and written again first, so fields added since it was published (e.g. the theme's
    // motion, or its fields since they were renamed) get the defaults guests are shown, and fields since removed (e.g.
    // the motion's section, text and media) are dropped, instead of counting as a change.
    public static bool HasUnpublishedChanges(this EventWebsite website)
    {
        if (website.PublishedContent is null)
            return false;

        var published = JsonSerializer.SerializeToNode(ReadPublishedContent(website.PublishedContent), PublishedContentJson);
        var current = JsonSerializer.SerializeToNode(website.ToPublishedWebsiteDto(), PublishedContentJson);
        return !JsonNode.DeepEquals(published, current);
    }

    private static WebsiteThemeDto ToWebsiteThemeDto(this EventWebsite website) => new()
    {
        PrimaryColor = website.PrimaryColor,
        SecondaryColor = website.SecondaryColor,
        AccentColor = website.AccentColor,
        BackgroundColor = website.BackgroundColor,
        FontPairingKey = website.FontPairingKey,
        CornerStyle = website.CornerStyle,
        HeroHeadingSize = website.HeroHeadingSize,
        HeadingSize = website.HeadingSize,
        Motion = new WebsiteMotionDto
        {
            Intro = website.MotionIntro,
            Intensity = website.MotionIntensity,
        },
    };

    public static WebsiteSectionDto ToWebsiteSectionDto(this WebsiteSection section)
    {
        return new WebsiteSectionDto
        {
            Id = section.Id,
            Type = section.Type,
            Layout = section.Layout,
            Background = section.Background,
            Options = new Dictionary<string, string>(section.Options),
            Content = JsonSerializer.Deserialize<JsonElement>(section.Content),
            ContentVersion = section.ContentVersion,
            IsVisible = section.IsVisible,
            Visibility = section.Visibility,
        };
    }
}
