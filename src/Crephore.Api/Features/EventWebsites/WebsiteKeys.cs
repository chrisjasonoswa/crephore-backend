namespace Crephore.Api.Features.EventWebsites;

/*
 * The web app's keys a website may use. They are defined in crephore-web and stored on saved websites,
 * so they are never renamed; add new ones here when the web app adds them:
 *   - section types and their layouts: features/sections/section.registry.ts
 *   - backgrounds: features/sections/section.type.ts (SectionBackground)
 *   - font pairings: site-theme/fontPairings.ts; corner styles: site-theme/useSiteTheme.ts (CornerStyle)
 *   - motion (page intros, intensities): features/site-motion/site-motion.type.ts
 */
public static class WebsiteKeys
{
    // Every website has exactly one hero section (its opening), shown or hidden
    public const string HeroSectionType = "hero";

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
            ["message"] = new HashSet<string>
            {
                "stacked", "split", "side-photo", "polaroid", "portrait", "card", "overlap", "postcard", "editorial", "inline",
                "photo-middle",
            },
            ["about"] = new HashSet<string>
            {
                "split", "photo-row", "mosaic", "divider", "portrait", "scrapbook", "magazine",
                "timeline-zigzag", "timeline-list", "timeline-cards", "timeline-polaroids", "timeline-compact",
            },
            ["people"] = new HashSet<string>
            {
                "grid", "entourage", "featured", "cards", "spotlight", "carousel", "staggered", "polaroids", "directory", "credits",
            },
            ["program"] = new HashSet<string>
            {
                "timeline", "list", "cards", "printed", "big-times", "horizontal", "poster", "agenda", "menu-card", "tickets",
                "table", "note", "vertical", "date-blocks", "photo-cards",
            },
        };

    public static readonly IReadOnlySet<string> Backgrounds = new HashSet<string> { "light", "photo", "primary", "secondary" };

    public static readonly IReadOnlySet<string> FontPairings = new HashSet<string>
    {
        // Weddings & engagements
        "serif_classic", "romantic", "timeless", "grand_ballroom", "modern_romance", "editorial", "garden", "vintage", "heirloom",
        "soft_serif", "marble", "chapel",
        // Script & calligraphy
        "script", "luxurious_script", "parisian", "love_letter", "calligraphy", "handwritten_vows", "ribbon", "formal_invitation",
        "sunset", "royal_script",
        // Modern & minimal
        "modern_sans", "minimal", "gallery", "clean", "architect", "studio", "art_deco", "journal",
        // Birthdays & parties
        "playful", "confetti", "party_pop", "diner", "bold_bash", "kids_party", "neon", "sketchbook", "sunny", "glam",
        // Business & conferences
        "plex", "keynote", "summit", "trusted", "startup", "annual_report",
        // Memorials & tributes
        "remembrance", "tribute", "quiet_light", "hymn",
    };

    public static readonly IReadOnlySet<string> CornerStyles = new HashSet<string> { "sharp", "soft", "round" };

    // The theme's motion: the intro when the site opens, and how strongly (the intensity scales the intro's speed).
    // No intro, then the scenes for any event, weddings, parties, business and memorials (20 each).
    public static readonly IReadOnlySet<string> PageIntros = new HashSet<string>
    {
        "none",
        "shutter", "film-strip", "card-flip", "stamp", "calendar", "pin-drop", "assemble", "color-bands", "orbit",
        "word-cycle", "paper-tear", "brush", "stardust", "window-light", "frosted-glass", "accordion", "date-roll",
        "focus-pull", "ribbon-tape", "counter-scroll",
        "envelope", "signature", "veil", "wax-seal", "ring-box", "golden-frame", "bloom", "meet", "light-leak", "pages",
        "ribbon", "polaroid", "toast", "doors", "vellum", "foil-press", "ink-wash", "aisle", "lace", "letter",
        "countdown", "balloon", "candle", "stickers", "gift-box", "disco", "popper", "marquee-lights", "bouncy", "slot",
        "champagne", "fireworks", "neon", "vinyl", "ticket-tear", "pinata", "photo-booth", "splash", "karaoke", "bunting",
        "counter", "split-title", "tiles", "kinetic", "blinds", "line-expand", "typewriter", "stack-cards", "scramble",
        "zoom-through", "badge", "blueprint", "spotlight", "aurora", "stats", "ribbon-cut", "split-flap", "globe", "keynote",
        "network",
        "wreath-seal", "candlelight", "dove", "portrait", "dates", "lily", "first-light", "mist", "verse", "lanterns",
        "feather", "album", "stars", "ripple", "butterfly", "after-rain", "tree", "sampaguita", "paper-boat", "bokeh",
    };

    public static readonly IReadOnlySet<string> MotionIntensities = new HashSet<string> { "subtle", "normal", "bold" };

    // What a website has until the host picks its motion (also what websites saved before motion existed get)
    public const string DefaultPageIntro = "shutter";
    public const string DefaultMotionIntensity = "bold";
}
