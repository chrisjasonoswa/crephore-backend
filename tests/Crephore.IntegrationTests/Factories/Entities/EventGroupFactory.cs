using Crephore.Api.Data;
using Crephore.Api.Data.Entities;

namespace Crephore.IntegrationTests.Factories.Entities;

public class EventGroupFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<EventGroup> CreateAsync(int ownerId, string? name = null)
    {
        var group = new EventGroup
        {
            OwnerId = ownerId,
            Name = name ?? $"Test Group {Guid.NewGuid().ToString("N")[..8]}",
        };

        _context.EventGroups.Add(group);
        await _context.SaveChangesAsync();

        return group;
    }
}
