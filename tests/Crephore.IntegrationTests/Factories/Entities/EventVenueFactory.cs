using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

// Venues for an event, in the given order (SortOrder from 0; the first is the main venue): "Main Venue" by default.
public class EventVenueFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<List<EventVenue>> CreateAsync(int eventId, params string[] names)
    {
        var venues = (names.Length > 0 ? names : ["Main Venue"])
            .Select((name, index) => new EventVenue
            {
                EventId = eventId,
                Name = name,
                Address = $"{index + 1} Test Street, Manila",
                SortOrder = index,
            })
            .ToList();

        _context.EventVenues.AddRange(venues);
        await _context.SaveChangesAsync();

        return venues;
    }
}
