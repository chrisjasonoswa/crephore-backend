namespace Crephore.Api.Features.EventWebsites;

/*
 * The web app's keys a website may use. They are defined in crephore-web and stored on saved websites,
 * so they are never renamed; add new ones here when the web app adds them:
 *   - section types and their layouts: features/sections/section.registry.ts
 *   - backgrounds: features/sections/section.type.ts (SectionBackground)
 *   - font pairings: site-theme/fontPairings.ts; corner styles: site-theme/useSiteTheme.ts (CornerStyle)
 */
public static class WebsiteKeys
{
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SectionLayouts =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["hero"] = new HashSet<string>
            {
                "fullscreen", "split", "arch", "editorial", "overlay-card", "collage", "banner", "filmstrip", "polaroid", "framed",
                "offset-frame",
            },
            ["countdown"] = new HashSet<string>
            {
                "boxes", "minimal", "photo", "split", "rings", "flip", "sentence", "days-only", "calendar", "strip",
            },
        };

    public static readonly IReadOnlySet<string> Backgrounds = new HashSet<string> { "light", "photo", "primary", "accent" };

    public static readonly IReadOnlySet<string> FontPairings = new HashSet<string> { "serif_classic", "modern_sans", "romantic", "playful" };

    public static readonly IReadOnlySet<string> CornerStyles = new HashSet<string> { "sharp", "soft", "round" };
}
