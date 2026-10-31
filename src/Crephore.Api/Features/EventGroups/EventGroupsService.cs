using Crephore.Api.Common.Api;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventGroups.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.EventGroups;

public interface IEventGroupsService
{
    Task<ApiPaginationResponseDto<EventGroupDto>> ListAsync(int ownerId, ListEventGroupsRequestDto request);
    Task<EventGroupDto> CreateAsync(int ownerId, CreateEventGroupRequestDto request);
    Task<EventGroupDto> UpdateAsync(int groupId, int ownerId, UpdateEventGroupRequestDto request);
    Task DeleteAsync(int groupId, int ownerId);
    Task<EventGroupDto> AddEventAsync(int groupId, int eventId, int ownerId);
    Task<EventGroupDto> RemoveEventAsync(int groupId, int eventId, int ownerId);
}

public class EventGroupsService(AppDbContext context) : IEventGroupsService
{
    private readonly AppDbContext _context = context;

    private const string NameTakenMessage = "Event group name already exists.";

    public async Task<ApiPaginationResponseDto<EventGroupDto>> ListAsync(int ownerId, ListEventGroupsRequestDto request)
    {
        IQueryable<EventGroup> query = _context.EventGroups
            .AsNoTracking()
            .Where(g => g.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(request.SearchKey))
        {
            var key = request.SearchKey.Trim();
            query = query.Where(g => g.Name.Contains(key));
        }

        query = query.ApplySort(
            request.Sort,
            fieldMap: new()
            {
                ["id"] = g => (object)g.Id,
                ["name"] = g => g.Name,
                ["eventcount"] = g => (object)g.EventUsers.Count,
                ["createdat"] = g => (object)g.CreatedAt,
            },
            defaultSort: g => g.Name,
            defaultDescending: false,
            tieBreaker: g => (object)g.Id
        );

        int totalCount = await query.CountAsync();

        var groups = await query
            .Include(g => g.EventUsers)
                .ThenInclude(eu => eu.Event)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync();

        return new ApiPaginationResponseDto<EventGroupDto>
        {
            Items = [.. groups.Select(g => g.ToEventGroupDto())],
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<EventGroupDto> CreateAsync(int ownerId, CreateEventGroupRequestDto request)
    {
        var name = request.Name.Trim();
        await ValidateNameIsUniqueAsync(ownerId, name);

        var group = new EventGroup
        {
            Name = name,
            OwnerId = ownerId,
        };

        _context.EventGroups.Add(group);
        if (request.EventIds is not null)
            await SetEventsAsync(group, ownerId, request.EventIds);

        await _context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventGroupOwnerName, "name", NameTakenMessage);

        return await GetDtoAsync(group.Id);
    }

    public async Task<EventGroupDto> UpdateAsync(int groupId, int ownerId, UpdateEventGroupRequestDto request)
    {
        var group = await FindGroupAsync(groupId, ownerId);

        var name = request.Name.Trim();
        await ValidateNameIsUniqueAsync(ownerId, name, groupId);

        group.Name = name;
        if (request.EventIds is not null)
            await SetEventsAsync(group, ownerId, request.EventIds);

        await _context.SaveChangesOrThrowIfDuplicateAsync(UniqueIndexes.EventGroupOwnerName, "name", NameTakenMessage);

        return await GetDtoAsync(groupId);
    }

    public async Task DeleteAsync(int groupId, int ownerId)
    {
        var group = await FindGroupAsync(groupId, ownerId);

        // Its events are ungrouped by the database (EventUser.EventGroupId set to null), not deleted
        _context.EventGroups.Remove(group);
        await _context.SaveChangesAsync();
    }

    // Any access to the event is enough: grouping only organises the user's own list
    public async Task<EventGroupDto> AddEventAsync(int groupId, int eventId, int ownerId)
    {
        await FindGroupAsync(groupId, ownerId);
        var eventUser = await FindEventUserAsync(eventId, ownerId);

        // An event is in at most one of the user's groups, so this also moves it out of its current group
        eventUser.EventGroupId = groupId;
        await _context.SaveChangesAsync();

        return await GetDtoAsync(groupId);
    }

    public async Task<EventGroupDto> RemoveEventAsync(int groupId, int eventId, int ownerId)
    {
        await FindGroupAsync(groupId, ownerId);
        var eventUser = await FindEventUserAsync(eventId, ownerId);

        if (eventUser.EventGroupId != groupId)
            throw new NotFoundException("Event is not in this group.");

        eventUser.EventGroupId = null;
        await _context.SaveChangesAsync();

        return await GetDtoAsync(groupId);
    }

    // Helpers
    private async Task<EventGroup> FindGroupAsync(int groupId, int ownerId)
    {
        return await _context.EventGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.OwnerId == ownerId)
            ?? throw new NotFoundException("Event group not found.");
    }

    private async Task<EventUser> FindEventUserAsync(int eventId, int userId)
    {
        return await _context.EventUsers
            .FirstOrDefaultAsync(eu => eu.EventId == eventId && eu.UserId == userId)
            ?? throw new NotFoundException("Event not found.");
    }

    private async Task<EventGroupDto> GetDtoAsync(int groupId)
    {
        var group = await _context.EventGroups
            .AsNoTracking()
            .Include(g => g.EventUsers)
                .ThenInclude(eu => eu.Event)
            .FirstAsync(g => g.Id == groupId);

        return group.ToEventGroupDto();
    }

    // Makes the group contain exactly `eventIds` (the user's own events): listed events move into it
    // (an event is in at most one of the user's groups), the group's other events are ungrouped.
    // Saved by the caller with the rest of the change.
    private async Task SetEventsAsync(EventGroup group, int userId, List<int> eventIds)
    {
        var ids = eventIds.Distinct().ToList();

        var selected = await _context.EventUsers
            .Where(eu => eu.UserId == userId && ids.Contains(eu.EventId))
            .ToListAsync();

        if (selected.Count != ids.Count)
            throw new AppValidationException(
                "Some events were not found.",
                new Dictionary<string, List<string>>
                {
                    ["eventIds"] = ["Some events were not found."],
                });

        foreach (var eventUser in selected)
            eventUser.EventGroup = group;

        // A new group has no events to remove yet
        if (group.Id == 0)
            return;

        var removed = await _context.EventUsers
            .Where(eu => eu.UserId == userId && eu.EventGroupId == group.Id && !ids.Contains(eu.EventId))
            .ToListAsync();

        foreach (var eventUser in removed)
        {
            eventUser.EventGroupId = null;
            eventUser.EventGroup = null;
        }
    }

    // Names are compared case-insensitively (database collation)
    private async Task ValidateNameIsUniqueAsync(int ownerId, string name, int? excludedGroupId = null)
    {
        var exists = await _context.EventGroups
            .AsNoTracking()
            .AnyAsync(g =>
                g.OwnerId == ownerId &&
                g.Name == name &&
                (excludedGroupId == null || g.Id != excludedGroupId));

        if (exists)
            throw new AppValidationException(
                NameTakenMessage,
                new Dictionary<string, List<string>>
                {
                    ["name"] = [NameTakenMessage],
                });
    }
}
