using System.Net;
using System.Net.Http.Headers;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Controllers;

public class AuthLogoutTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task Logout_ShouldReturnOkAndClearCookies_WhenRequestIsAuthenticated()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var setCookieValues).Should().BeTrue();

        var setCookies = setCookieValues!.ToList();
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.AccessToken}="));
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.RefreshToken}=") && c.Contains($"path=/{ApiRoutes.Auth.Refresh.FullPath}"));
        setCookies.Should().Contain(c => c.StartsWith($"{AuthCookies.SessionId}="));
    }

    [Fact]
    public async Task Logout_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(ApiRoutes.Auth.Logout.FullPath, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
