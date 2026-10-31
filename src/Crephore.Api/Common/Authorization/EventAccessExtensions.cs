using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Common.Authorization;

public static class EventAccessExtensions
{
    public const string EventNotFoundMessage = "Event not found.";

    // Owner grants every feature; any other value grants only itself.
    public static bool Grants(this IEnumerable<EventAccess> access, EventAccess required)
    {
        return access.Contains(EventAccess.Owner) || access.Contains(required);
    }

    // The user's access to the event. 404 when they have none at all (its existence is not revealed),
    // 403 when they can see it but their access does not grant `required` (null: any access is enough).
    public static async Task<EventUser> RequireEventAccessAsync(
        this AppDbContext context,
        int eventId,
        int userId,
        EventAccess? required = null)
    {
        var eventUser = await context.EventUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(eu => eu.EventId == eventId && eu.UserId == userId)
            ?? throw new NotFoundException(EventNotFoundMessage);

        if (required is not null && !eventUser.Access.Grants(required.Value))
            throw new ForbiddenException("You do not have access to do this.");

        return eventUser;
    }
}
