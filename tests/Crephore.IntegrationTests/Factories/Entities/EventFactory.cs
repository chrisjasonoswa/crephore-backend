using Crephore.Api.Common.Constants;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

public class EventFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<Event> CreateAsync(
        int ownerId,
        string? name = null,
        int? eventGroupId = null,
        DateTimeOffset? startsAt = null,
        string? subdomain = null,
        List<string>? timeline = null,
        EventType type = EventType.Birthday
    )
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var start = startsAt ?? new DateTimeOffset(2027, 6, 12, 15, 0, 0, TimeSpan.Zero);

        var @event = new Event
        {
            Name = name ?? $"Test Event {suffix}",
            Type = type,
            VenueName = "Test Venue",
            VenueAddress = "1 Test Street, Manila",
            StartsAt = start,
            EndsAt = start.AddHours(6),
            Subdomain = subdomain ?? $"event-{suffix}",
            Timezone = "Asia/Manila",
            Timeline = timeline ?? [],
        };

        // The owner's access, and their group when given
        @event.EventUsers.Add(new EventUser
        {
            UserId = ownerId,
            Access = [EventAccess.Owner],
            EventGroupId = eventGroupId,
        });

        _context.Events.Add(@event);
        await _context.SaveChangesAsync();

        return @event;
    }
}
