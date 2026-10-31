using System.Net;
using Crephore.Api.Common.Api;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Crephore.IntegrationTests.Tests.EventGroups.Controllers;

public class DeleteEventGroupTests(DatabaseFixture fixture)
    : EventGroupsBaseTestClass(fixture)
{
    [Fact]
    public async Task Delete_ShouldReturnOk_WhenOwnerDeletesTheGroup()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var group = await EventGroupFactory.CreateAsync(user.Id);

        var response = await client.DeleteAsync($"{ApiRoutes.EventGroups.FullPath}/{group.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync($"{ApiRoutes.EventGroups.FullPath}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
