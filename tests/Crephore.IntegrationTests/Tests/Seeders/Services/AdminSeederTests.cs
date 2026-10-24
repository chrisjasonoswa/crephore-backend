using Crephore.Api.Data.Seeders;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Seeders.Services;

public class AdminSeederTests(DatabaseFixture fixture)
    : SeedersBaseTestClass(fixture)
{
    private static DefaultAdminOptions BuildOptions() => new()
    {
        Email = "admin@crephore.local",
        Password = "Admin12345!",
        FirstName = "Crephore",
        LastName = "Admin",
    };

    [Fact]
    public async Task ShouldCreateSuperAdmin_WhenConfiguredAndUserDoesNotExist()
    {
        var options = BuildOptions();

        await AdminSeeder.SeedAsync(options);

        var admin = await UserManager.FindByEmailAsync(options.Email!);
        admin.Should().NotBeNull();
        admin!.IsSuperAdmin.Should().BeTrue();
        admin.EmailConfirmed.Should().BeTrue();
        admin.FirstName.Should().Be("Crephore");
        admin.LastName.Should().Be("Admin");
        (await UserManager.CheckPasswordAsync(admin, options.Password!)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldNotCreateDuplicate_WhenRunMoreThanOnce()
    {
        var options = BuildOptions();

        await AdminSeeder.SeedAsync(options);
        await AdminSeeder.SeedAsync(options);

        (await Context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ShouldPromoteWithoutChangingPassword_WhenUserAlreadyExists()
    {
        var existingUser = await AppUserFactory.CreateAsync(email: "admin@crephore.local");

        await AdminSeeder.SeedAsync(BuildOptions());

        var userInDb = await Context.Users.AsNoTracking().FirstAsync(u => u.Id == existingUser.Id);
        userInDb.IsSuperAdmin.Should().BeTrue();
        (await UserManager.CheckPasswordAsync(userInDb, AppUserFactory.GetValidPassword)).Should().BeTrue();
        (await UserManager.CheckPasswordAsync(userInDb, "Admin12345!")).Should().BeFalse();
        (await Context.Users.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(null, "Admin12345!")]
    [InlineData("admin@crephore.local", null)]
    [InlineData("  ", "  ")]
    public async Task ShouldSkip_WhenEmailOrPasswordIsNotConfigured(string? email, string? password)
    {
        var options = BuildOptions();
        options.Email = email;
        options.Password = password;

        await AdminSeeder.SeedAsync(options);

        (await Context.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldThrowInvalidOperationException_WhenPasswordFailsPolicy()
    {
        var options = BuildOptions();
        options.Password = "short";

        var act = async () => await AdminSeeder.SeedAsync(options);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to create the default admin*");
        (await Context.Users.AnyAsync()).Should().BeFalse();
    }
}
