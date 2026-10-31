using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.Auth.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Controllers;

public class AuthLoginTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task Login_ShouldReturnOkTokenAndCookies_WhenCredentialsAreValid()
    {
        var user = await AppUserFactory.CreateAsync();
        using var client = CreateClient();

        var request = new LoginRequestDto
        {
            Email = user.Email!,
            Password = AppUserFactory.GetValidPassword
        };

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Login.FullPath, request);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var data = content?.Data;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        data.SessionId.Should().NotBeNullOrWhiteSpace();
        data.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        data.User.Id.Should().Be(user.Id);

        response.Headers.TryGetValues("Set-Cookie", out var setCookieValues).Should().BeTrue();
        var setCookies = setCookieValues!.ToList();
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.AccessToken}=") && c.Contains("httponly"));
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.RefreshToken}=") && c.Contains("httponly")
            // Only sent to the refresh endpoint
            && c.Contains($"path=/{ApiRoutes.Auth.Refresh.FullPath}"));
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.SessionId}="));
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        var user = await AppUserFactory.CreateAsync();
        using var client = CreateClient();

        var request = new LoginRequestDto
        {
            Email = user.Email!,
            Password = "WrongPassword123"
        };

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Login.FullPath, request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result!.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenRequiredFieldsAreEmpty()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Login.FullPath, new LoginRequestDto());
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKeys("email", "password");
    }
}
