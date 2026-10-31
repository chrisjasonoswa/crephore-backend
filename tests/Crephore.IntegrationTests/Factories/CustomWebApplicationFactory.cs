using Crephore.Api.Data;
using Crephore.IntegrationTests.Factories.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace Crephore.IntegrationTests.Factories;

public class CustomWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;

    // Uploaded files go to a temporary folder of their own, deleted with the factory
    public string StorageRootPath { get; } = Path.Combine(Path.GetTempPath(), $"crephore-test-uploads_{Guid.NewGuid().ToString("N")[..8]}");
    public const string StoragePublicBaseUrl = "http://localhost/uploads";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tests run as "Development"; keep the dev default admin out of the test databases.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DefaultAdmin:Email"] = "",
                ["DefaultAdmin:Password"] = "",
                ["Storage:Provider"] = "Local",
                ["Storage:Local:RootPath"] = StorageRootPath,
                ["Storage:Local:RequestPath"] = "/uploads",
                ["Storage:Local:PublicBaseUrl"] = StoragePublicBaseUrl,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Point the app at the temporary test database
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseMySql(
                    _connectionString,
                    ServerVersion.AutoDetect(_connectionString)
                );
            });

            // Register Factories
            // REMINDER: Also expose new factories in BaseTestClass.
            services.AddScoped<AppUserFactory>();
            services.AddScoped<AppUserTokenFactory>();
            services.AddScoped<EventFactory>();
            services.AddScoped<EventGroupFactory>();
            services.AddScoped<EventUserFactory>();
            services.AddScoped<EventWebsiteFactory>();
        });

        builder.UseEnvironment("Development");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(StorageRootPath))
            Directory.Delete(StorageRootPath, recursive: true);
    }
}
