using Crephore.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Crephore.Api.Data.Seeders;

// Bound from the "DefaultAdmin" configuration section.
public class DefaultAdminOptions
{
    public const string SectionName = "DefaultAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public interface IAdminSeeder
{
    Task SeedAsync(DefaultAdminOptions options);
}

/*
 * Ensures a super admin exists, since public signup can never create one.
 * Idempotent:
 *  - skipped when Email or Password is not configured
 *  - an existing user with that email is promoted to super admin; their password is never changed
 *  - otherwise the user is created through UserManager (same password policy and hashing as signup)
 */
public class AdminSeeder(
    UserManager<AppUser> userManager,
    ILogger<AdminSeeder> logger
) : IAdminSeeder
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly ILogger<AdminSeeder> _logger = logger;

    public async Task SeedAsync(DefaultAdminOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            _logger.LogInformation("Default admin not configured; skipping seeding.");
            return;
        }

        string email = options.Email.Trim();

        AppUser? existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            if (existingUser.IsSuperAdmin)
                return;

            existingUser.IsSuperAdmin = true;
            EnsureSucceeded(await _userManager.UpdateAsync(existingUser), "promote the default admin");

            _logger.LogInformation("Promoted existing user {Email} to super admin.", email);
            return;
        }

        var admin = new AppUser
        {
            FirstName = string.IsNullOrWhiteSpace(options.FirstName) ? "Admin" : options.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(options.LastName) ? "User" : options.LastName.Trim(),
            Email = email,
            // Identity requires a UserName; users log in by email (same as signup).
            UserName = $"user_{Guid.NewGuid():N}",
            EmailConfirmed = true,
            IsSuperAdmin = true,
        };

        EnsureSucceeded(await _userManager.CreateAsync(admin, options.Password), "create the default admin");

        _logger.LogInformation("Created default super admin {Email}.", email);
    }

    // Misconfiguration (e.g. a password that fails the policy) should be loud, not silently ignored.
    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
