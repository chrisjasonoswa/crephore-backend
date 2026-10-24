using Crephore.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Crephore.IntegrationTests.Factories.Entities;

public class AppUserFactory(UserManager<AppUser> userManager)
{
    private readonly UserManager<AppUser> _userManager = userManager;

    public string GetValidPassword => "StrongPassword123";

    public async Task<AppUser> CreateAsync(
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? phoneNumber = null,
        bool isSuperAdmin = false
    )
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var appUser = new AppUser
        {
            FirstName = firstName ?? "Test",
            LastName = lastName ?? "User",
            Email = email ?? $"user_{suffix}@test.com",
            UserName = $"user_{suffix}",
            PhoneNumber = phoneNumber,
            IsSuperAdmin = isSuperAdmin,
        };

        var result = await _userManager.CreateAsync(appUser, GetValidPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create test user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return appUser;
    }
}
