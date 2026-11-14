using System.Text;
using System.Text.Json;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventWebsites.Dtos;
using Crephore.Api.Features.Media;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.EventWebsites;

public interface IEventWebsitesService
{
    Task<EventWebsiteDto?> GetAsync(int eventId, int userId);
    Task<EventWebsiteDto> UpdateAsync(int eventId, int userId, UpdateEventWebsiteRequestDto request);
    Task DeleteAsync(int eventId, int userId);
    Task<EventWebsiteDto> PublishAsync(int eventId, int userId);
    Task<EventWebsiteDto> UnpublishAsync(int eventId, int userId);
}

public class EventWebsitesService(AppDbContext context, IMediaService mediaService) : IEventWebsitesService
{
    private readonly AppDbContext _context = context;
    private readonly IMediaService _mediaService = mediaService;

    public const int MaxSections = 50;
    public const int ContentMaxBytes = 64 * 1024;
    public const int MaxOptions = 20;
    public const int OptionMaxLength = 64;

    // The event's website, or null when the host hasn't saved one yet. Any access can read it.
    public async Task<EventWebsiteDto?> GetAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId);

        var website = await _context.EventWebsites
            .AsNoTracking()
            .Include(w => w.Sections)
            .FirstOrDefaultAsync(w => w.EventId == eventId);

        return website is null ? null : await ToDtoAsync(website);
    }

    // Creates the website on its first save. Replaces the theme and the sections: sections with an id are updated,
    // new ones added, and saved ones left out deleted; their order is the request's.
    public async Task<EventWebsiteDto> UpdateAsync(int eventId, int userId, UpdateEventWebsiteRequestDto request)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        var website = await _context.EventWebsites
            .Include(w => w.Sections)
            .FirstOrDefaultAsync(w => w.EventId == eventId);

        // The photos the sections use (references to the event's media): unknown ones are errors, and the rest
        // get their URLs in the response
        var mediaUrls = await _mediaService.GetUrlsAsync(eventId, request.Sections
            .Where(s => s.Content.ValueKind == JsonValueKind.Object)
            .SelectMany(s => MediaReferences.IdsIn(s.Content)));

        var contents = Validate(request, website, mediaUrls);

        if (website is null)
        {
            website = new EventWebsite { EventId = eventId };
            _context.EventWebsites.Add(website);
        }

        var theme = request.Theme;
        website.PrimaryColor = theme.PrimaryColor.ToUpperInvariant();
        website.SecondaryColor = OptionalColor(theme.SecondaryColor);
        website.AccentColor = OptionalColor(theme.AccentColor);
        website.BackgroundColor = theme.BackgroundColor?.ToUpperInvariant();
        website.FontPairingKey = theme.FontPairingKey;
        website.CornerStyle = theme.CornerStyle;
        website.HeroHeadingSize = theme.HeroHeadingSize;
        website.HeadingSize = theme.HeadingSize;
        website.EyebrowStyle = theme.EyebrowStyle;
        website.MotionIntro = theme.Motion.Intro;
        website.MotionIntensity = theme.Motion.Intensity;

        var keptIds = request.Sections.Where(s => s.Id is not null).Select(s => s.Id!.Value).ToHashSet();
        foreach (var removed in website.Sections.Where(s => !keptIds.Contains(s.Id)).ToList())
        {
            website.Sections.Remove(removed);
            _context.WebsiteSections.Remove(removed);
        }

        var saved = website.Sections.ToDictionary(s => s.Id);
        foreach (var (item, index) in request.Sections.Select((item, index) => (item, index)))
        {
            if (item.Id is not { } id || !saved.TryGetValue(id, out var section))
            {
                section = new WebsiteSection();
                website.Sections.Add(section);
            }

            section.Type = item.Type;
            section.Layout = item.Layout;
            section.Background = item.Background;
            section.Options = new Dictionary<string, string>(item.Options);
            section.Content = contents[index];
            section.ContentVersion = item.ContentVersion;
            section.IsVisible = item.IsVisible;
            section.Visibility = item.Visibility;
            section.SortOrder = index;
        }

        // The first save from two places at once: the second would be a second website for the event
        await _context.SaveChangesOrThrowIfDuplicateAsync(
            UniqueIndexes.EventWebsiteEvent,
            "website",
            "This website was just saved somewhere else. Reload the page to see it.");

        return website.ToEventWebsiteDto(mediaUrls);
    }

    // Removes the website and its sections (the host starts over). Its uploaded photos stay in the event's media.
    public async Task DeleteAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        var deleted = await _context.EventWebsites.Where(w => w.EventId == eventId).ExecuteDeleteAsync();
        if (deleted == 0)
            throw new NotFoundException("This event has no website yet.");
    }

    // Guests now see the saved website as it is (its visible sections), until it is published again or unpublished
    public async Task<EventWebsiteDto> PublishAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);
        var website = await FindSavedWebsiteAsync(eventId, "Save your website before publishing it.");

        if (!website.Sections.Any(s => s.IsVisible))
        {
            const string message = "Add at least one visible section before publishing.";
            throw new AppValidationException(message, new Dictionary<string, List<string>> { ["sections"] = [message] });
        }

        website.PublishedContent = website.ToPublishedContent();
        website.PublishedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return await ToDtoAsync(website);
    }

    // Guests no longer see the website; the saved website stays
    public async Task<EventWebsiteDto> UnpublishAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);
        var website = await FindSavedWebsiteAsync(eventId, "This event has no website yet.");

        website.PublishedContent = null;
        website.PublishedAt = null;
        await _context.SaveChangesAsync();

        return await ToDtoAsync(website);
    }

    // Helpers
    // The website with the URLs of the photos its sections use. Expects Sections to be loaded.
    private async Task<EventWebsiteDto> ToDtoAsync(EventWebsite website)
    {
        var mediaUrls = await _mediaService.GetUrlsAsync(website.EventId,
            website.Sections.SelectMany(s => MediaReferences.IdsIn(s.Content)));
        return website.ToEventWebsiteDto(mediaUrls);
    }

    // An optional colour: left empty (or blank) is none
    private static string? OptionalColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? null : color.ToUpperInvariant();

    private async Task<EventWebsite> FindSavedWebsiteAsync(int eventId, string notFoundMessage) =>
        await _context.EventWebsites
            .Include(w => w.Sections)
            .FirstOrDefaultAsync(w => w.EventId == eventId)
        ?? throw new NotFoundException(notFoundMessage);

    // Throws an AppValidationException with every error. Sections have no fields in the editor to show errors on,
    // so the first error is also the message (the web app shows it as a toast). Returns each section's content as
    // stored: its photos reduced to their media id. `mediaUrls` holds the event's media among the photos' ids.
    private static List<string> Validate(UpdateEventWebsiteRequestDto request, EventWebsite? website,
        IReadOnlyDictionary<int, string> mediaUrls)
    {
        var contents = new List<string>();
        var errors = new Dictionary<string, List<string>>();

        if (!WebsiteKeys.FontPairings.Contains(request.Theme.FontPairingKey))
            AddError(errors, "fontPairingKey", "The font pairing is not one Crephore offers.");
        if (!WebsiteKeys.CornerStyles.Contains(request.Theme.CornerStyle))
            AddError(errors, "cornerStyle", "The corner style must be sharp, soft or round.");
        if (!WebsiteKeys.HeadingSizes.Contains(request.Theme.HeroHeadingSize))
            AddError(errors, "heroHeadingSize", "The hero heading size must be small, medium, large or extra large.");
        if (!WebsiteKeys.HeadingSizes.Contains(request.Theme.HeadingSize))
            AddError(errors, "headingSize", "The heading size must be small, medium, large or extra large.");
        if (!WebsiteKeys.EyebrowStyles.Contains(request.Theme.EyebrowStyle))
            AddError(errors, "eyebrowStyle", "The eyebrow style must be plain or boxed.");
        if (!WebsiteKeys.PageIntros.Contains(request.Theme.Motion.Intro))
            AddError(errors, "motion.intro", "The intro animation is not one Crephore offers.");
        if (!WebsiteKeys.MotionIntensities.Contains(request.Theme.Motion.Intensity))
            AddError(errors, "motion.intensity", "The motion intensity must be subtle, normal or bold.");

        var savedIds = website?.Sections.Select(s => s.Id).ToHashSet() ?? [];
        var requestedIds = new HashSet<int>();

        foreach (var (section, index) in request.Sections.Select((section, index) => (section, index)))
        {
            var name = $"Section {index + 1}";

            if (section.Id is { } id && (!savedIds.Contains(id) || !requestedIds.Add(id)))
                AddError(errors, "sections", $"{name} is no longer on this website. Reload the page to see the latest version.");

            if (!WebsiteKeys.SectionLayouts.TryGetValue(section.Type, out var layouts))
                AddError(errors, "sections", $"{name} has an unknown section type.");
            else if (!layouts.Contains(section.Layout))
                AddError(errors, "sections", $"{name} has an unknown layout.");

            if (!WebsiteKeys.Backgrounds.Contains(section.Background))
                AddError(errors, "sections", $"{name} has an unknown background.");

            if (section.Options.Count > MaxOptions ||
                section.Options.Any(o => o.Key.Length > OptionMaxLength || o.Value is null || o.Value.Length > OptionMaxLength))
                AddError(errors, "sections", $"{name} has invalid layout options.");

            if (section.Content.ValueKind != JsonValueKind.Object)
                AddError(errors, "sections", $"{name} has no content.");
            else if (Encoding.UTF8.GetByteCount(section.Content.GetRawText()) > ContentMaxBytes)
                AddError(errors, "sections", $"{name} has too much content.");
            else if (!MediaReferences.TryToStored(section.Content, out var stored, out var mediaIds))
                AddError(errors, "sections", $"{name} has a photo that isn't valid.");
            else if (!mediaIds.All(mediaUrls.ContainsKey))
                AddError(errors, "sections", $"{name} has a photo that is no longer available. Upload it again.");
            else
            {
                contents.Add(stored.ToJsonString());
                continue;
            }
            contents.Add(string.Empty);
        }

        var heroCount = request.Sections.Count(s => s.Type == WebsiteKeys.HeroSectionType);
        if (heroCount == 0)
            AddError(errors, "sections", "Add a cover section: every website needs one.");
        else if (heroCount > 1)
            AddError(errors, "sections", "A website can only have one cover section.");

        if (errors.Count > 0)
            throw new AppValidationException(errors.Values.First().First(), errors);

        return contents;
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
            errors[field] = messages = [];
        messages.Add(message);
    }
}
