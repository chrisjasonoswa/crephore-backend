using System.Text.RegularExpressions;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Common.Utilities;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.Events.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.Events;

public interface IEventsService
{
    Task<ApiPaginationResponseDto<EventDto>> ListAsync(int userId, ListEventsRequestDto request);
    Task<EventDto> GetAsync(int eventId, int userId);
    Task<EventDto> CreateAsync(int userId, CreateEventRequestDto request);
    Task DeleteAsync(int eventId, int userId);
    Task<EventDto> UpdateAsync(int eventId, int userId, UpdateEventRequestDto request);
}

public partial class EventsService(AppDbContext context) : IEventsService
{
    private readonly AppDbContext _context = context;

    // 3–63 characters: lowercase letters, numbers and hyphens, not starting or ending with a hyphen
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$")]
    private static partial Regex SubdomainRegex();

    private const string SubdomainTakenMessage = "This website address is already taken.";
    private const string InvalidTimezoneMessage = "The timezone must be an IANA time zone, e.g. \"Asia/Manila\".";

    // Every event the user has access to (any access), with their own group
    public async Task<ApiPaginationResponseDto<EventDto>> ListAsync(int userId, ListEventsRequestDto request)
    {
        IQueryable<EventUser> query = _context.EventUsers
            .AsNoTracking()
            .Where(eu => eu.UserId == userId);

        if (request.EventGroupId is not null)
            query = query.Where(eu => eu.EventGroupId == request.EventGroupId);

        if (!string.IsNullOrWhiteSpace(request.SearchKey))
        {
            var key = request.SearchKey.Trim();
            query = query.Where(eu => eu.Event.Name.Contains(key) || eu.Event.Subdomain.Contains(key));
        }

        query = query.ApplySort(
            request.Sort,
            fieldMap: new()
            {
                ["id"] = eu => (object)eu.EventId,
                ["name"] = eu => eu.Event.Name,
                ["type"] = eu => (object)eu.Event.Type,
                ["startsat"] = eu => (object)eu.Event.StartsAt,
                ["createdat"] = eu => (object)eu.Event.CreatedAt,
                ["subdomain"] = eu => eu.Event.Subdomain,
            },
            defaultSort: eu => (object)eu.Event.StartsAt,
            defaultDescending: false,
            tieBreaker: eu => (object)eu.EventId
        );

        int totalCount = await query.CountAsync();

        var rows = await query
            .Include(eu => eu.EventGroup)
            .Include(eu => eu.Event)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(eu => new
            {
                EventUser = eu,
                // The main venue's name, in the same query
                VenueName = eu.Event.Venues.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => v.Name).FirstOrDefault(),
            })
            .ToListAsync();

        return new ApiPaginationResponseDto<EventDto>
        {
            Items = [.. rows.Select(row => row.EventUser.ToEventDto(row.VenueName ?? string.Empty))],
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<EventDto> GetAsync(int eventId, int userId)
    {
        var row = await _context.EventUsers
            .AsNoTracking()
            .Include(eu => eu.EventGroup)
            .Include(eu => eu.Event)
            .Where(eu => eu.EventId == eventId && eu.UserId == userId)
            .Select(eu => new
            {
                EventUser = eu,
                VenueName = eu.Event.Venues.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => v.Name).FirstOrDefault(),
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException(EventAccessExtensions.EventNotFoundMessage);

        return row.EventUser.ToEventDto(row.VenueName ?? string.Empty);
    }

    // The creator gets Owner access
    public async Task<EventDto> CreateAsync(int userId, CreateEventRequestDto request)
    {
        var (values, group) = await ValidateAsync(userId, request, request.EventGroupId);

        var @event = new Event();
        SetFields(@event, request, values);

        var eventUser = new EventUser
        {
            UserId = userId,
            Event = @event,
            Access = [EventAccess.Owner],
            EventGroup = group,
        };

        _context.EventUsers.Add(eventUser);
        await _context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventSubdomain, "subdomain", SubdomainTakenMessage);

        // A new event has no venues yet
        return eventUser.ToEventDto(venueName: string.Empty);
    }

    // Replaces every field of the event; the user's group stays as it is (changed through the groups endpoints)
    public async Task<EventDto> UpdateAsync(int eventId, int userId, UpdateEventRequestDto request)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);
        var (values, _) = await ValidateAsync(userId, request, eventGroupId: null, eventId);

        var eventUser = await _context.EventUsers
            .Include(eu => eu.Event)
            .Include(eu => eu.EventGroup)
            .FirstAsync(eu => eu.EventId == eventId && eu.UserId == userId);

        SetFields(eventUser.Event, request, values);

        await _context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventSubdomain, "subdomain", SubdomainTakenMessage);

        var venueName = await _context.EventVenues
            .Where(v => v.EventId == eventId)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .Select(v => v.Name)
            .FirstOrDefaultAsync();

        return eventUser.ToEventDto(venueName ?? string.Empty);
    }

    public async Task DeleteAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        // Its event users are deleted by the database (cascade)
        await _context.Events.Where(e => e.Id == eventId).ExecuteDeleteAsync();
    }

    // Helpers
    // Checked and normalized request values, ready to set on an event
    private record ValidatedEvent(
        string Subdomain,
        DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt,
        TimeZoneInfo TimeZone);

    // Throws an AppValidationException with every field error. `eventGroupId` is the group a new event goes into (the
    // user's own, returned); `eventId` is the event being updated (its own subdomain is not "taken").
    private async Task<(ValidatedEvent Values, EventGroup? Group)> ValidateAsync(
        int userId, EventRequestDto request, int? eventGroupId, int? eventId = null)
    {
        var errors = new Dictionary<string, List<string>>();
        var subdomain = request.Subdomain.Trim().ToLowerInvariant();

        if (!SubdomainRegex().IsMatch(subdomain))
            AddError(errors, "subdomain", "The website address may only contain lowercase letters, numbers and hyphens.");

        // Times arrive as wall-clock times at the venue and are stored in UTC
        DateTimeOffset? startsAt = null, endsAt = null;
        if (!TimeZones.TryFind(request.Timezone, out var timeZone))
        {
            AddError(errors, "timezone", InvalidTimezoneMessage);
        }
        else
        {
            startsAt = ToUtc(request.StartsAt!.Value, timeZone, errors, "startsAt", "start");
            // Optional: only checked when given
            if (request.EndsAt is { } end)
                endsAt = ToUtc(end, timeZone, errors, "endsAt", "end");
            if (endsAt < startsAt)
                AddError(errors, "endsAt", "The end must not be before the start.");
        }

        // Only a memorial's dates count (other types save none)
        if (request.Type == EventType.Memorial && request.DiedOn < request.BornOn)
            AddError(errors, "diedOn", "The date of passing must not be before the date of birth.");

        if (!errors.ContainsKey("subdomain") &&
            await _context.Events.AnyAsync(e => e.Subdomain == subdomain && (eventId == null || e.Id != eventId)))
            AddError(errors, "subdomain", SubdomainTakenMessage);

        EventGroup? group = null;
        if (eventGroupId is not null)
        {
            group = await _context.EventGroups
                .FirstOrDefaultAsync(g => g.Id == eventGroupId && g.OwnerId == userId);

            if (group is null)
                AddError(errors, "eventGroupId", "Event group not found.");
        }

        if (errors.Count > 0)
            throw new AppValidationException(errors);

        return (new ValidatedEvent(subdomain, startsAt!.Value, endsAt, timeZone!), group);
    }

    private static void SetFields(Event @event, EventRequestDto request, ValidatedEvent values)
    {
        @event.Name = request.Name.Trim();
        @event.Type = request.Type!.Value;
        @event.StartsAt = values.StartsAt;
        @event.EndsAt = values.EndsAt;
        @event.Timezone = values.TimeZone.Id;
        @event.Subdomain = values.Subdomain;
        var isMemorial = @event.Type == EventType.Memorial;
        @event.BornOn = isMemorial ? request.BornOn : null;
        @event.DiedOn = isMemorial ? request.DiedOn : null;
    }

        // A wall-clock time (no offset) that must exist in `timeZone` → UTC; adds a field error otherwise
    private static DateTimeOffset? ToUtc(
        DateTime wallClock,
        TimeZoneInfo timeZone,
        Dictionary<string, List<string>> errors,
        string field,
        string label)
    {
        if (wallClock.Kind != DateTimeKind.Unspecified)
        {
            AddError(errors, field, $"The {label} must be a local date and time without an offset.");
            return null;
        }

        var utc = TimeZones.ToUtc(wallClock, timeZone);
        if (utc is null)
            AddError(errors, field, $"The {label} does not exist in this timezone (the clocks skip it).");

        return utc;
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
            errors[field] = messages = [];
        messages.Add(message);
    }
}
