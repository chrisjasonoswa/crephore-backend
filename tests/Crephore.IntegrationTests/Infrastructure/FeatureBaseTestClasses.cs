using Crephore.IntegrationTests.Fixtures;

namespace Crephore.IntegrationTests.Infrastructure;

[Collection(TestCollections.Auth)]
public abstract class AuthBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);

[Collection(TestCollections.Seeders)]
public abstract class SeedersBaseTestClass(DatabaseFixture fixture) : BaseTestClass(fixture);
