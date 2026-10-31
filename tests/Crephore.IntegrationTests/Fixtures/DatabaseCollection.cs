namespace Crephore.IntegrationTests.Fixtures;

// One collection (and therefore one temporary database) per feature.
// Test classes in different collections run in parallel.
public static class TestCollections
{
    public const string Auth = "Auth Database";
    public const string Events = "Events Database";
    public const string EventGroups = "EventGroups Database";
    public const string Seeders = "Seeders Database";
    public const string Data = "Data Database";
    public const string EventWebsites = "EventWebsites Database";
    public const string Media = "Media Database";
    public const string Sites = "Sites Database";
}

[CollectionDefinition(TestCollections.Auth)]
public class AuthDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.Seeders)]
public class SeedersDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.Events)]
public class EventsDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.EventGroups)]
public class EventGroupsDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.Data)]
public class DataDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.EventWebsites)]
public class EventWebsitesDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.Media)]
public class MediaDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.Sites)]
public class SitesDatabaseCollection : ICollectionFixture<DatabaseFixture>;
