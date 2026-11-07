using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

// Program items for an event, in the given order (SortOrder from 0): a 3:00 PM item on the event's day by default,
// without a venue unless one is given.
public class EventProgramItemFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public Task<List<EventProgramItem>> CreateAsync(int eventId, params string[] titles) =>
        CreateAtVenueAsync(eventId, venueId: null, titles);

    // Every item at the given venue (one of the event's)
    public async Task<List<EventProgramItem>> CreateAtVenueAsync(int eventId, int? venueId, params string[] titles)
    {
        var items = (titles.Length > 0 ? titles : ["Ceremony"])
            .Select((title, index) => new EventProgramItem
            {
                EventId = eventId,
                Date = new DateOnly(2027, 6, 12),
                StartTime = new TimeOnly(15, 0).AddHours(index),
                Title = title,
                VenueId = venueId,
                SortOrder = index,
            })
            .ToList();

        _context.EventProgramItems.AddRange(items);
        await _context.SaveChangesAsync();

        return items;
    }
}
