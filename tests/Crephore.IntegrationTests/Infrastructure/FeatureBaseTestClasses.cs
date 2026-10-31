using Crephore.IntegrationTests.Fixtures;

namespace Crephore.IntegrationTests.Infrastructure;

[Collection(TestCollections.Auth)]
public abstract class AuthBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.Seeders)]
public abstract class SeedersBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.Events)]
public abstract class EventsBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.EventGroups)]
public abstract class EventGroupsBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.Data)]
public abstract class DataBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.EventWebsites)]
public abstract class EventWebsitesBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.Media)]
public abstract class MediaBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.Sites)]
public abstract class SitesBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);
