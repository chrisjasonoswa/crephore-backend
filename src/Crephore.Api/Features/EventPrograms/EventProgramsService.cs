using System.Globalization;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventPrograms.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.EventPrograms;

public interface IEventProgramsService
{
    Task<EventProgramDto> GetAsync(int eventId, int userId);
    Task<EventProgramDto> UpdateAsync(int eventId, int userId, UpdateEventProgramRequestDto request);
}

public class EventProgramsService(AppDbContext context) : IEventProgramsService
{
    private readonly AppDbContext _context = context;

    public const int MaxItems = 40;
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 500;
    public const int SpeakersMaxLength = 300;

    // The event's program (empty until the host adds items). Any access can read it.
    public async Task<EventProgramDto> GetAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId);

        var items = await _context.EventProgramItems
            .AsNoTracking()
            .Where(i => i.EventId == eventId)
            .ToListAsync();

        return EventProgramsMapper.ToEventProgramDto(items);
    }

    // Replaces the program: items with an id are updated, new ones added, and saved ones left out deleted; their order
    // is the request's.
    public async Task<EventProgramDto> UpdateAsync(int eventId, int userId, UpdateEventProgramRequestDto request)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        var @event = await _context.Events
            .Include(e => e.ProgramItems)
            .FirstAsync(e => e.Id == eventId);

        var venueIds = await _context.EventVenues
            .Where(v => v.EventId == eventId)
            .Select(v => v.Id)
            .ToListAsync();

        var values = Validate(request, @event, venueIds.ToHashSet());

        var keptIds = request.Items.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var removed in @event.ProgramItems.Where(i => !keptIds.Contains(i.Id)).ToList())
        {
            @event.ProgramItems.Remove(removed);
            _context.EventProgramItems.Remove(removed);
        }

        var saved = @event.ProgramItems.ToDictionary(i => i.Id);
        foreach (var (item, index) in request.Items.Select((item, index) => (item, index)))
        {
            if (item.Id is not { } id || !saved.TryGetValue(id, out var programItem))
            {
                programItem = new EventProgramItem();
                @event.ProgramItems.Add(programItem);
            }

            var (date, startTime, endTime) = values[index];
            programItem.Date = date;
            programItem.StartTime = startTime;
            programItem.EndTime = endTime;
            programItem.Title = item.Title!.Trim();
            programItem.Description = Optional(item.Description);
            programItem.VenueId = item.VenueId;
            programItem.Speakers = Optional(item.Speakers);
            programItem.SortOrder = index;
        }

        await _context.SaveChangesAsync();

        return EventProgramsMapper.ToEventProgramDto(@event.ProgramItems);
    }

    // Helpers
    // Optional text: left empty (or blank) is none
    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Throws an AppValidationException with every error, keyed by the item's position ("items[2].title"). Returns each
    // item's parsed date and times.
    private static List<(DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime)> Validate(
        UpdateEventProgramRequestDto request, Event @event, HashSet<int> venueIds)
    {
        var errors = new Dictionary<string, List<string>>();
        var values = new List<(DateOnly, TimeOnly?, TimeOnly?)>();

        var savedIds = @event.ProgramItems.Select(i => i.Id).ToHashSet();
        var requestedIds = new HashSet<int>();

        foreach (var (item, index) in request.Items.Select((item, index) => (item, index)))
        {
            var key = $"items[{index}]";

            if (item.Id is { } id && (!savedIds.Contains(id) || !requestedIds.Add(id)))
                AddError(errors, "items", $"Item {index + 1} is no longer in the program. Reload the page to see the latest version.");

            var date = default(DateOnly);
            if (string.IsNullOrWhiteSpace(item.Date))
                AddError(errors, $"{key}.date", "Date is required.");
            else if (!DateOnly.TryParseExact(item.Date, EventProgramsMapper.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                AddError(errors, $"{key}.date", "Date is not a valid date.");

            var startTime = ParseTime(item.StartTime, $"{key}.startTime", "Start time", errors);
            var endTime = ParseTime(item.EndTime, $"{key}.endTime", "End time", errors);
            if (endTime is not null && string.IsNullOrWhiteSpace(item.StartTime))
                AddError(errors, $"{key}.endTime", "Add a start time first.");

            if (string.IsNullOrWhiteSpace(item.Title))
                AddError(errors, $"{key}.title", "Title is required.");
            else if (item.Title.Trim().Length > TitleMaxLength)
                AddError(errors, $"{key}.title", $"Title must be {TitleMaxLength} characters or fewer.");

            CheckLength(item.Description, DescriptionMaxLength, $"{key}.description", "Note", errors);
            if (item.VenueId is { } venueId && !venueIds.Contains(venueId))
                AddError(errors, $"{key}.venueId", "Pick one of the event's venues.");
            CheckLength(item.Speakers, SpeakersMaxLength, $"{key}.speakers", "Speakers", errors);

            values.Add((date, startTime, endTime));
        }

        if (errors.Count > 0)
            throw new AppValidationException(errors);

        return values;
    }

    // "15:00", or null when left empty
    private static TimeOnly? ParseTime(string? value, string key, string label, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (TimeOnly.TryParseExact(value, EventProgramsMapper.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return time;

        AddError(errors, key, $"{label} must be a time like 15:00.");
        return null;
    }

    private static void CheckLength(string? value, int maxLength, string key, string label, Dictionary<string, List<string>> errors)
    {
        if (value is not null && value.Trim().Length > maxLength)
            AddError(errors, key, $"{label} must be {maxLength} characters or fewer.");
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
            errors[key] = messages = [];
        messages.Add(message);
    }
}
