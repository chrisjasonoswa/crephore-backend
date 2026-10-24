using System.Net;
using System.Net.Http.Headers;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Infrastructure;

/*
 * Covers the JWT bearer pipeline (ProgramServiceExtensions.AddAuthentication):
 * tokens are read from the header or cookie, and rejected once their session is revoked or expired,
 * or once the user's security stamp changes.
 * Uses the logout endpoint as a representative [Authorize] endpoint.
 */
public class SessionValidationTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task ShouldAuthenticate_WhenAccessTokenIsSentViaCookie()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        using var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auth.Logout.FullPath);
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

        var firstResponse = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);
        var secondResponse = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturnUnauthorized_WhenSessionIsExpired()
    {
        var user = await AppUserFactory.CreateAsync();
        var session = await AppUserSessionFactory.CreateAsync(user, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var accessToken = TokenProvider.CreateToken(user, session.SessionId);

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

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

        var response = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturnUnauthorized_WhenSessionDoesNotExist()
    {
        var user = await AppUserFactory.CreateAsync();
        var accessToken = TokenProvider.CreateToken(user, Guid.NewGuid().ToString());

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
