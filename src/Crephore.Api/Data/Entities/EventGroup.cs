using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

// A user's own grouping of related events (through their EventUser rows).
// Deleting a group ungroups its events; it never deletes them.
[Table("event_groups")]
public class EventGroup : IHasCreatedAt
{
    public int Id { get; set; }

    // Unique per owner
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }

    /* Related Properties */
    public int OwnerId { get; set; }
    public AppUser Owner { get; set; } = null!;

    public ICollection<EventUser> EventUsers { get; set; } = new List<EventUser>();
}
