using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.Auth.Dtos;
using Crephore.Api.Features.AppUsers.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Controllers;

public class AuthSignupTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task Signup_ShouldReturnOk_WhenAllFieldsAreValid()
    {
        using var client = CreateClient();

        var request = new SignupRequestDto
        {
            FirstName = "Chris",
            LastName = "Oswa",
            Email = "chris@test.com",
            Password = "StrongPassword123",
        };

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Signup.FullPath, request);
        var body = await response.Content.ReadAsStringAsync();
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<AppUserDto>>();
        var data = content?.Data;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content!.Success.Should().BeTrue();
        data.Should().NotBeNull();
        data!.Id.Should().BeGreaterThan(0);
        data.FirstName.Should().Be(request.FirstName);
        data.LastName.Should().Be(request.LastName);
        data.Email.Should().Be(request.Email);
        data.Timezone.Should().Be("UTC");

        // Never expose the password hash
        body.Should().NotContainEquivalentOf("password");
    }

    [Fact]
    public async Task Signup_ShouldReturnBadRequest_WhenRequiredFieldsAreEmpty()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Signup.FullPath, new SignupRequestDto());
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Be("Some fields are invalid.");
        result.Errors.Should().ContainKeys("firstName", "lastName", "email", "password");
    }

    [Fact]
    public async Task Signup_ShouldReturnBadRequest_WhenEmailFormatIsInvalidAndPasswordIsTooShort()
    {
        using var client = CreateClient();

        var request = new SignupRequestDto
        {
            FirstName = "Chris",
            LastName = "Oswa",
            Email = "invalid-email",
            Password = "short1",
        };

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Signup.FullPath, request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!["email"].Should().Contain("The email field is not a valid e-mail address.");
        result.Errors["password"].Should().Contain("The password must be at least 8 characters.");
    }

    [Fact]
    public async Task Signup_ShouldReturnBadRequest_WhenEmailAlreadyExists()
    {
        var existingUser = await AppUserFactory.CreateAsync();
        using var client = CreateClient();

        var request = new SignupRequestDto
        {
            FirstName = "Chris",
            LastName = "Oswa",
            Email = existingUser.Email!,
            Password = "StrongPassword123",
        };

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Signup.FullPath, request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Success.Should().BeFalse();
        result.Errors.Should().ContainKey("email");
    }
}
