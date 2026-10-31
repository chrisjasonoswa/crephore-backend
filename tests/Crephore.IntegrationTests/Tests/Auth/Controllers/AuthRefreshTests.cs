using System.Net;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.Auth.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Controllers;

public class AuthRefreshTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task Refresh_ShouldReturnOkAndSetNewCookies_WhenRefreshTokenCookieIsValid()
    {
        var user = await AppUserFactory.CreateAsync();
        var (_, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Refresh.FullPath);
        request.Headers.Add("Cookie", $"{AuthCookies.RefreshToken}={Uri.EscapeDataString(refreshToken)}");

        var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RefreshResponseDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.RefreshToken.Should().NotBe(refreshToken);

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.AccessToken}=") && c.Contains("httponly"));
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.RefreshToken}=") && c.Contains("httponly")
            // Only sent to the refresh endpoint
            && c.Contains($"path=/{ApiRoutes.Auth.Refresh.FullPath}"));
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.SessionId}="));
    }

    [Fact]
    public async Task Refresh_ShouldReturnOk_WhenRefreshTokenIsSentInTheBody()
    {
        var user = await AppUserFactory.CreateAsync();
        var (_, refreshToken) = await AppUserTokenFactory.CreateAsync(user);
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Refresh.FullPath, new RefreshRequestDto { RefreshToken = refreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorized_WhenNoRefreshTokenIsSent()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Refresh.FullPath, new { });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result!.Message.Should().Be("Your session has expired.");
    }
}
