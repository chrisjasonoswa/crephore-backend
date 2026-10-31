using Crephore.Api.Common.Constants;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

// Gives another user access to an existing event (EventFactory already adds the owner's row).
public class EventUserFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<EventUser> CreateAsync(int eventId, int userId, List<EventAccess>? access = null, int? eventGroupId = null)
    {
        var eventUser = new EventUser
        {
            EventId = eventId,
            UserId = userId,
            Access = access ?? [],
            EventGroupId = eventGroupId,
        };

        _context.EventUsers.Add(eventUser);
        await _context.SaveChangesAsync();

        return eventUser;
    }
}
