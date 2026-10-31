using Crephore.Api.Common.Storage;
using Crephore.Api.Data;
using Crephore.Api.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

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

    // With local storage, the API serves the uploaded files itself (e.g. /uploads/events/12/3f2a….jpg).
    // Only files the API stored are there, all images it checked, so their extension gives a safe content type.
    public static void UseLocalFileStorage(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
        if (options.Provider != StorageOptions.LocalProvider)
            return;

        var rootPath = LocalFileStorage.GetRootPath(options.Local, app.Environment);
        Directory.CreateDirectory(rootPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(rootPath),
            RequestPath = options.Local.RequestPath,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                // File names are unique and never reused, so browsers can keep them
                context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            },
        });
    }
}
