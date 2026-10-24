namespace Crephore.IntegrationTests.Fixtures;

// One collection (and therefore one temporary database) per feature.
// Test classes in different collections run in parallel.
public static class TestCollections
{
    public const string Auth = "Auth Database";
    public const string Seeders = "Seeders Database";
}

[CollectionDefinition(TestCollections.Auth)]
public class AuthDatabaseCollection : ICollectionFixture<DatabaseFixture>;

[CollectionDefinition(TestCollections.Seeders)]
public class SeedersDatabaseCollection : ICollectionFixture<DatabaseFixture>;
