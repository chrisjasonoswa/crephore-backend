using System.Text;
using System.Text.Json;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventWebsites.Dtos;
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

public class EventWebsitesService(AppDbContext context) : IEventWebsitesService
{
    private readonly AppDbContext _context = context;

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

        return website?.ToEventWebsiteDto();
    }

    // Creates the website on its first save. Replaces the theme and the sections: sections with an id are updated,
    // new ones added, and saved ones left out deleted; their order is the request's.
    public async Task<EventWebsiteDto> UpdateAsync(int eventId, int userId, UpdateEventWebsiteRequestDto request)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        var website = await _context.EventWebsites
            .Include(w => w.Sections)
            .FirstOrDefaultAsync(w => w.EventId == eventId);

        Validate(request, website);

        if (website is null)
        {
            website = new EventWebsite { EventId = eventId };
            _context.EventWebsites.Add(website);
        }

        var theme = request.Theme;
        website.PrimaryColor = theme.PrimaryColor.ToUpperInvariant();
        website.AccentColor = theme.AccentColor.ToUpperInvariant();
        website.BackgroundColor = theme.BackgroundColor?.ToUpperInvariant();
        website.FontPairingKey = theme.FontPairingKey;
        website.CornerStyle = theme.CornerStyle;

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
            section.Content = item.Content.GetRawText();
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

        return website.ToEventWebsiteDto();
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

        return website.ToEventWebsiteDto();
    }

    // Guests no longer see the website; the saved website stays
    public async Task<EventWebsiteDto> UnpublishAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);
        var website = await FindSavedWebsiteAsync(eventId, "This event has no website yet.");

        website.PublishedContent = null;
        website.PublishedAt = null;
        await _context.SaveChangesAsync();

        return website.ToEventWebsiteDto();
    }

    // Helpers
    private async Task<EventWebsite> FindSavedWebsiteAsync(int eventId, string notFoundMessage) =>
        await _context.EventWebsites
            .Include(w => w.Sections)
            .FirstOrDefaultAsync(w => w.EventId == eventId)
        ?? throw new NotFoundException(notFoundMessage);

    // Throws an AppValidationException with every error. Sections have no fields in the editor to show errors on,
    // so the first error is also the message (the web app shows it as a toast).
    private static void Validate(UpdateEventWebsiteRequestDto request, EventWebsite? website)
    {
        var errors = new Dictionary<string, List<string>>();

        if (!WebsiteKeys.FontPairings.Contains(request.Theme.FontPairingKey))
            AddError(errors, "fontPairingKey", "The font pairing is not one Crephore offers.");
        if (!WebsiteKeys.CornerStyles.Contains(request.Theme.CornerStyle))
            AddError(errors, "cornerStyle", "The corner style must be sharp, soft or round.");

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
        }

        if (errors.Count > 0)
            throw new AppValidationException(errors.Values.First().First(), errors);
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
            errors[field] = messages = [];
        messages.Add(message);
    }
}
