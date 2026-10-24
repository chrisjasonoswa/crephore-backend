using Crephore.IntegrationTests.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Respawn;
using Crephore.Api.Data;


namespace Crephore.IntegrationTests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private Respawner? _respawner;

    private const string _rootDbName = "crephore-mysql-test";
    private readonly string _databaseName;
    private readonly string _rootConnectionString;

    public string ConnectionString { get; }

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public DatabaseFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.LocalTest.json", optional: false)
            .Build();

        var baseConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Test connection string not found.");

        var builder = new MySqlConnectionStringBuilder(baseConnectionString);
        _databaseName = $"{_rootDbName}_{Guid.NewGuid().ToString("N")[..8]}";

        // Database connection to the whole mysql server (without specifying a database) is needed to create and drop databases.
        builder.Database = string.Empty;
        _rootConnectionString = builder.ConnectionString;

        // Database connection to the test database, used by the application and tests.
        builder.Database = _databaseName;
        ConnectionString = builder.ConnectionString;
    }


    public async Task InitializeAsync()
    {
        await CreateDatabaseAsync();

        Factory = new CustomWebApplicationFactory(ConnectionString);
        await InitializeDatabaseAsync();
        await InitializeRespawnerAsync();
    }

    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            return;
        }

        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();

        // When db fixture is disposed, delete the test database to clean up resources.
        await DropDatabaseAsync();
    }


    // Helper methods
    private async Task CreateDatabaseAsync()
    {
        await using var connection = new MySqlConnection(_rootConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{_databaseName}`;";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        await using var connection = new MySqlConnection(_rootConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
        await command.ExecuteNonQueryAsync();
    }


    private async Task InitializeDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
    }

    private async Task InitializeRespawnerAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.MySql,
            SchemasToInclude = [_databaseName],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }
}
