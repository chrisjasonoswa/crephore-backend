using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crephore.Api.Common.Api;
using Crephore.Api.Features.Media.Dtos;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using ServiceTests = Crephore.IntegrationTests.Tests.Media.Services.CreateMediaTests;

namespace Crephore.IntegrationTests.Tests.Media.Controllers;

public class CreateMediaTests(DatabaseFixture fixture)
    : MediaBaseTestClass(fixture)
{
    // Like a browser upload: the photo in `file`
    private static MultipartFormDataContent BuildForm(byte[] bytes, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "photo.png" } };
    }

    [Fact]
    public async Task Create_ShouldReturnOkAndServeTheStoredPhoto()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PostAsync(ApiRoutes.Media.FullPath(@event.Id), BuildForm(ServiceTests.Png));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MediaDto>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Data!.ContentType.Should().Be("image/png");

        // The API serves the file at its URL (local storage)
        var photo = await CreateClient().GetAsync(new Uri(result.Data.Url).AbsolutePath);
        photo.StatusCode.Should().Be(HttpStatusCode.OK);
        photo.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        photo.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        (await photo.Content.ReadAsByteArrayAsync()).Should().Equal(ServiceTests.Png);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenNoFileIsSent()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        var response = await client.PostAsync(ApiRoutes.Media.FullPath(@event.Id), new MultipartFormDataContent { { new StringContent("x"), "other" } });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors.Should().ContainKey("file");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenTheFileIsNotAPhoto()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);

        // Claims to be a PNG, but isn't one
        var response = await client.PostAsync(ApiRoutes.Media.FullPath(@event.Id), BuildForm("<html></html>"u8.ToArray()));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Message.Should().Be("The photo must be a JPG, PNG, WebP or GIF image.");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenThePhotoIsOverTheLimit()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var @event = await EventFactory.CreateAsync(user.Id);
        var tooLarge = new byte[Crephore.Api.Features.Media.MediaService.PhotoMaxBytes + 1];
        ServiceTests.Png.CopyTo(tooLarge, 0);

        var response = await client.PostAsync(ApiRoutes.Media.FullPath(@event.Id), BuildForm(tooLarge));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result!.Errors!["file"].Should().Equal("The photo must be 10 MB or smaller.");
    }

    [Fact]
    public async Task Create_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using var _ = client;
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, user.Id);

        var response = await client.PostAsync(ApiRoutes.Media.FullPath(@event.Id), BuildForm(ServiceTests.Png));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenRequestHasNoToken()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(ApiRoutes.Media.FullPath(1), BuildForm(ServiceTests.Png));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
