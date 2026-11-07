using System.Text.Json.Nodes;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.EventWebsites.Dtos;

namespace Crephore.IntegrationTests.Factories.Entities;

// A saved website for an event, with the given sections in page order (a hero by default) and the given motion (the
// defaults when left out); published as it is when asked.
public class EventWebsiteFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<EventWebsite> CreateAsync(int eventId, List<WebsiteSection>? sections = null, bool published = false,
        WebsiteMotionDto? motion = null)
    {
        motion ??= new WebsiteMotionDto();
        var website = new EventWebsite
        {
            EventId = eventId,
            PrimaryColor = "#1E2A44",
            SecondaryColor = "#E8A598",
            AccentColor = "#C9A227",
            FontPairingKey = "serif_classic",
            CornerStyle = "soft",
            MotionIntro = motion.Intro,
            MotionIntensity = motion.Intensity,
        };

        foreach (var (section, index) in (sections ?? [BuildSection()]).Select((section, index) => (section, index)))
        {
            section.SortOrder = index;
            website.Sections.Add(section);
        }

        if (published)
        {
            website.PublishedContent = website.ToPublishedContent();
            website.PublishedAt = DateTimeOffset.UtcNow;
        }

        _context.EventWebsites.Add(website);
        await _context.SaveChangesAsync();

        return website;
    }

    // A published copy as stored before websites had motion: the theme has no "motion"
    public static string WithoutMotion(string publishedContent)
    {
        var content = JsonNode.Parse(publishedContent)!;
        content["theme"]!.AsObject().Remove("motion");
        return content.ToJsonString();
    }

    // A published copy as stored with motion's first shape, { transition, animation, intensity }, before it became
    // { intro, intensity }
    public static string WithOldMotion(string publishedContent, string transition = "orbit", string animation = "pop")
    {
        var content = JsonNode.Parse(publishedContent)!;
        content["theme"]!["motion"] = new JsonObject
        {
            ["transition"] = transition,
            ["animation"] = animation,
            ["intensity"] = WebsiteKeys.DefaultMotionIntensity,
        };
        return content.ToJsonString();
    }

    // A published copy as stored while motion also had scroll animations, { intro, section, text, media, intensity },
    // with the motion's current intro and intensity
    public static string WithScrollMotion(string publishedContent, string section = "tilt", string text = "letters",
        string media = "parallax")
    {
        var content = JsonNode.Parse(publishedContent)!;
        var motion = content["theme"]!["motion"]!.AsObject();
        motion["section"] = section;
        motion["text"] = text;
        motion["media"] = media;
        return content.ToJsonString();
    }

    public static WebsiteSection BuildSection(string type = "hero", string layout = "split", string heading = "Ana & Marco", bool isVisible = true) => new()
    {
        Type = type,
        Layout = layout,
        Background = "light",
        Options = new Dictionary<string, string> { ["photoSide"] = "right" },
        Content = $$"""{ "heading": "{{heading}}", "layoutPhotos": [] }""",
        ContentVersion = 1,
        IsVisible = isVisible,
    };
}
