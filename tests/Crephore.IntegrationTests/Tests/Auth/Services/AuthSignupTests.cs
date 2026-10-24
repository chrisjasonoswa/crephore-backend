using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.Auth.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Auth.Services;

public class AuthSignupTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    private static SignupRequestDto BuildValidRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new SignupRequestDto
        {
            FirstName = "Chris",
            LastName = "Oswa",
            Email = $"chris_{suffix}@test.com",
            Password = "StrongPassword123",
            PhoneNumber = "+639171234567",
        };
    }

    [Fact]
    public async Task ShouldCreateUserWithHashedPassword_WhenRequestIsValid()
    {
        var request = BuildValidRequest();

        var result = await AuthService.SignupAsync(request);

        // Check service response
        result.Id.Should().BeGreaterThan(0);
        result.FirstName.Should().Be(request.FirstName);
        result.LastName.Should().Be(request.LastName);
        result.Email.Should().Be(request.Email);
        result.PhoneNumber.Should().Be(request.PhoneNumber);
        result.IsSuperAdmin.Should().BeFalse();

        // Check user in db
        var userInDb = await Context.Users.AsNoTracking().FirstAsync(u => u.Id == result.Id);
        userInDb.Email.Should().Be(request.Email);
        userInDb.IsSuperAdmin.Should().BeFalse();
        userInDb.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        userInDb.NormalizedEmail.Should().Be(request.Email.ToUpperInvariant());
        userInDb.UserName.Should().StartWith("user_");

        // Password must be hashed, never stored as plain text
        userInDb.PasswordHash.Should().NotBe(request.Password);
        (await UserManager.CheckPasswordAsync(userInDb, request.Password)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldTrimEmail_WhenEmailHasWhitespace()
    {
        var request = BuildValidRequest();
        var expectedEmail = request.Email;
        request.Email = $"  {request.Email}  ";

        var result = await AuthService.SignupAsync(request);

        result.Email.Should().Be(expectedEmail);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenEmailAlreadyExists()
    {
        var existingUser = await AppUserFactory.CreateAsync();

        var request = BuildValidRequest();
        request.Email = existingUser.Email!.ToUpperInvariant();

        var act = async () => await AuthService.SignupAsync(request);

        var exception = await act.Should().ThrowAsync<AppValidationException>();
        exception.Which.Message.Should().Be("Email already exists");
        exception.Which.Errors.Should().ContainKey("email");

        (await Context.Users.CountAsync()).Should().Be(1);
    }
}
