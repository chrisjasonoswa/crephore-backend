using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Common.Constants;
using Crephore.Api.Data.IEntities;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Data.Entities;

/*
 * A user's access to an event. Whoever creates the event gets ["Owner"].
 * A user only sees events they have a row for; what they can do is decided by Access.
 */
[Table("event_users")]
[PrimaryKey(nameof(UserId), nameof(EventId))]
public class EventUser : IHasCreatedAt
{
    // JSON array of EventAccess names, e.g. ["Owner"]
    public List<EventAccess> Access { get; set; } = [];

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }

    /* Related Properties */
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    // This user's own group for the event (groups are personal); null when ungrouped
    public int? EventGroupId { get; set; }
    public EventGroup? EventGroup { get; set; }
}
