namespace Crephore.Api.Data;

// Names of the unique indexes that services map to field errors (see DbContextExtensions).
// They equal EF's default names, so naming them in OnModelCreating does not change the schema.
public static class UniqueIndexes
{
    public const string EventSubdomain = "IX_events_Subdomain";
    public const string EventGroupOwnerName = "IX_event_groups_OwnerId_Name";
    public const string EventWebsiteEvent = "IX_event_websites_EventId";
}
