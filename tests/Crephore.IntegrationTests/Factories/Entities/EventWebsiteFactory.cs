using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventWebsites;

namespace Crephore.IntegrationTests.Factories.Entities;

// A saved website for an event, with the given sections in page order (a hero by default); published as it is when asked.
public class EventWebsiteFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<EventWebsite> CreateAsync(int eventId, List<WebsiteSection>? sections = null, bool published = false)
    {
        var website = new EventWebsite
        {
            EventId = eventId,
            PrimaryColor = "#1E2A44",
            AccentColor = "#E8A598",
            FontPairingKey = "serif_classic",
            CornerStyle = "soft",
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

    public static WebsiteSection BuildSection(string type = "hero", string layout = "split", string title = "Ana & Marco", bool isVisible = true) => new()
    {
        Type = type,
        Layout = layout,
        Background = "light",
        Options = new Dictionary<string, string> { ["photoSide"] = "right" },
        Content = $$"""{ "title": "{{title}}", "layoutPhotos": [] }""",
        ContentVersion = 1,
        IsVisible = isVisible,
    };
}
