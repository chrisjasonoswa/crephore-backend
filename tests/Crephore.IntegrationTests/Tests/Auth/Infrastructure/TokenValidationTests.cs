using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.Auth.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Infrastructure;

/*
 * Covers the JWT bearer pipeline (ProgramServiceExtensions.AddAuthentication): tokens are read from the header
 * or cookie, and rejected once their session is logged out (this device only) or the user's security stamp
 * changes (password change/reset, everywhere). Uses GET auth/me as a representative [Authorize] endpoint.
 */
public class TokenValidationTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldAuthenticate_WhenAccessTokenIsSentViaCookie()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        using var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Auth.Me.FullPath);
        request.Headers.Add("Cookie", $"{AuthCookies.AccessToken}={accessToken}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldReturnUnauthorized_WhenTokenIsReusedAfterLogout()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var logout = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);
        var afterLogout = await client.GetAsync(ApiRoutes.Auth.Me.FullPath);

        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldKeepOtherDevicesSignedIn_AfterLogout()
    {
        var user = await AppUserFactory.CreateAsync();
        var (phoneAccessToken, phoneRefreshToken) = await CreateAuthenticatedSessionAsync(user);
        var (laptopAccessToken, _) = await CreateAuthenticatedSessionAsync(user);

        using var laptop = CreateClient();
        laptop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", laptopAccessToken);
        await laptop.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

        // Logging out on the laptop leaves the phone's access and refresh tokens working
        using var phone = CreateClient();
        phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", phoneAccessToken);
        (await phone.GetAsync(ApiRoutes.Auth.Me.FullPath)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await laptop.GetAsync(ApiRoutes.Auth.Me.FullPath)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var phoneRefresh = CreateClient();
        var refresh = await phoneRefresh.PostAsJsonAsync(ApiRoutes.Auth.Refresh.FullPath, new RefreshRequestDto { RefreshToken = phoneRefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldReturnUnauthorized_WhenTheSessionHasNoActiveRefreshToken()
    {
        var user = await AppUserFactory.CreateAsync();
        var accessToken = TokenProvider.CreateToken(user, Guid.NewGuid().ToString());

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync(ApiRoutes.Auth.Me.FullPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturnUnauthorized_WhenSecurityStampChanged()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        // e.g. password change/reset: signs the user out everywhere
        await UserManager.UpdateSecurityStampAsync(user);

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync(ApiRoutes.Auth.Me.FullPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldAuthenticateAgain_WithTheAccessTokenFromARefresh()
    {
        var user = await AppUserFactory.CreateAsync();
        var (_, refreshToken) = await CreateAuthenticatedSessionAsync(user);

        using var client = CreateClient();
        var refresh = await client.PostAsJsonAsync(ApiRoutes.Auth.Refresh.FullPath, new RefreshRequestDto { RefreshToken = refreshToken });
        var accessToken = (await refresh.Content.ReadFromJsonAsync<ApiResponse<RefreshResponseDto>>())!.Data!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync(ApiRoutes.Auth.Me.FullPath);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
