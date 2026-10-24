using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.AppUsers.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.Auth.Controllers;

public class GetAuthMeTests(DatabaseFixture fixture)
    : AuthBaseTestClass(fixture)
{
    [Fact]
    public async Task GetMe_ShouldReturnOkAndCurrentUser_WhenRequestIsAuthenticated()
    {
        var user = await AppUserFactory.CreateAsync();
        var (accessToken, _) = await CreateAuthenticatedSessionAsync(user);

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync(ApiRoutes.Auth.Me.FullPath);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<AppUserDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content!.Success.Should().BeTrue();
        content.Data!.Id.Should().Be(user.Id);
        content.Data.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetMe_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(ApiRoutes.Auth.Me.FullPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
