using Crephore.Api.Data;
using Crephore.Api.Data.Seeders;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Extensions;

public static class WebApplicationExtensions
{
    // Seeds the default super admin from the "DefaultAdmin" configuration section.
    // A missing or unmigrated database is logged instead of stopping the app from starting.
    public static async Task SeedDefaultAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<AdminSeeder>>();

        var options = app.Configuration
            .GetSection(DefaultAdminOptions.SectionName)
            .Get<DefaultAdminOptions>() ?? new DefaultAdminOptions();

        if (string.IsNullOrWhiteSpace(options.Email))
            return;

        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                logger.LogWarning("Skipping default admin seeding: the database has pending migrations. Run `dotnet ef database update`.");
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping default admin seeding: the database is not reachable. Run `dotnet ef database update`.");
            return;
        }

        await services.GetRequiredService<IAdminSeeder>().SeedAsync(options);
    }
}
