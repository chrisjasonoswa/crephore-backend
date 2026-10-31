using System.Text;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.Media;
using Crephore.IntegrationTests.Factories;
using Crephore.IntegrationTests.Fixtures;
using Crephore.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Crephore.IntegrationTests.Tests.Media.Services;

public class CreateMediaTests(DatabaseFixture fixture)
    : MediaBaseTestClass(fixture)
{
    // A 1×1 PNG
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private string StoredPath(string key) => Path.Combine(Fixture.Factory.StorageRootPath, key);

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 }, "image/jpeg", ".jpg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0 }, "image/gif", ".gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56 }, "image/webp", ".webp")]
    public async Task ShouldStoreThePhotoWithItsDetectedFormat_WhenItIsAJpgGifOrWebp(byte[] bytes, string contentType, string extension)
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var media = await MediaService.CreatePhotoAsync(@event.Id, owner.Id, new MemoryStream(bytes), bytes.Length);

        media.ContentType.Should().Be(contentType);
        media.Url.Should().EndWith(extension);
    }

    [Fact]
    public async Task ShouldStoreThePhotoAndRecordIt_WhenOwnerUploadsAPng()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var media = await MediaService.CreatePhotoAsync(@event.Id, owner.Id, new MemoryStream(Png), Png.Length);

        media.Type.Should().Be(MediaType.Photo);
        media.Purpose.Should().Be(MediaPurpose.WebsiteAsset);
        media.ContentType.Should().Be("image/png");
        media.SizeBytes.Should().Be(Png.Length);
        media.Url.Should().MatchRegex($"^{CustomWebApplicationFactory.StoragePublicBaseUrl}/events/{@event.Id}/[0-9a-f]{{32}}\\.png$");

        var saved = await Context.Media.AsNoTracking().SingleAsync();
        saved.EventId.Should().Be(@event.Id);
        saved.UploaderUserId.Should().Be(owner.Id);
        (await File.ReadAllBytesAsync(StoredPath(saved.StorageKey))).Should().Equal(Png);
    }

    [Fact]
    public async Task ShouldDeleteTheEventsMedia_WhenTheEventIsDeleted()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await MediaService.CreatePhotoAsync(@event.Id, owner.Id, new MemoryStream(Png), Png.Length);

        await EventsService.DeleteAsync(@event.Id, owner.Id);

        (await Context.Media.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenTheFileIsNotAPhoto()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        var act = async () => await MediaService.CreatePhotoAsync(@event.Id, owner.Id, new MemoryStream(svg), svg.Length);

        (await act.Should().ThrowAsync<AppValidationException>()).Which.Errors!["file"].Should()
            .Equal("The photo must be a JPG, PNG, WebP or GIF image.");
        (await Context.Media.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowValidationException_WhenThePhotoIsEmptyOrTooLarge()
    {
        var owner = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        var tooLarge = new byte[Crephore.Api.Features.Media.MediaService.PhotoMaxBytes + 1];
        Png.CopyTo(tooLarge, 0);

        var empty = async () => await MediaService.CreatePhotoAsync(@event.Id, owner.Id, new MemoryStream(), 0);
        var large = async () => await MediaService.CreatePhotoAsync(@event.Id, owner.Id, new MemoryStream(tooLarge), tooLarge.Length);

        (await empty.Should().ThrowAsync<AppValidationException>()).Which.Message.Should().Be("The photo is empty.");
        (await large.Should().ThrowAsync<AppValidationException>()).Which.Message.Should().Be("The photo must be 10 MB or smaller.");
    }

    [Fact]
    public async Task ShouldThrowForbiddenException_WhenUserIsNotTheOwner()
    {
        var owner = await AppUserFactory.CreateAsync();
        var member = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);
        await EventUserFactory.CreateAsync(@event.Id, member.Id);

        var act = async () => await MediaService.CreatePhotoAsync(@event.Id, member.Id, new MemoryStream(Png), Png.Length);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await Context.Media.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShouldThrowNotFoundException_WhenEventBelongsToAnotherUser()
    {
        var owner = await AppUserFactory.CreateAsync();
        var otherUser = await AppUserFactory.CreateAsync();
        var @event = await EventFactory.CreateAsync(owner.Id);

        var act = async () => await MediaService.CreatePhotoAsync(@event.Id, otherUser.Id, new MemoryStream(Png), Png.Length);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
