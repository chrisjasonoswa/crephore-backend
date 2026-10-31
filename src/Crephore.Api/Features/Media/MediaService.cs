using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Common.Storage;
using Crephore.Api.Data;
using Crephore.Api.Features.Media.Dtos;
using MediaEntity = Crephore.Api.Data.Entities.Media;

namespace Crephore.Api.Features.Media;

public interface IMediaService
{
    Task<MediaDto> CreatePhotoAsync(int eventId, int userId, Stream content, long length);
}

public class MediaService(AppDbContext context, IFileStorage storage, ILogger<MediaService> logger) : IMediaService
{
    private readonly AppDbContext _context = context;
    private readonly IFileStorage _storage = storage;
    private readonly ILogger<MediaService> _logger = logger;

    public const long PhotoMaxBytes = 10 * 1024 * 1024;
    // Uploads up to this size reach the service, so a photo over PhotoMaxBytes gets its own message;
    // larger requests are turned away while they are read
    public const long RequestMaxBytes = 30 * 1024 * 1024;

    private record PhotoFormat(string ContentType, string Extension);

    // A photo for the event's website. The format is read from the file's first bytes, never from its name or the
    // request's content type, so only real JPEG, PNG, WebP and GIF images are stored (no SVG: it can run scripts).
    public async Task<MediaDto> CreatePhotoAsync(int eventId, int userId, Stream content, long length)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        if (length == 0)
            throw FileError("The photo is empty.");
        if (length > PhotoMaxBytes)
            throw FileError("The photo must be 10 MB or smaller.");

        var format = await DetectPhotoAsync(content)
            ?? throw FileError("The photo must be a JPG, PNG, WebP or GIF image.");

        var key = $"events/{eventId}/{Guid.NewGuid():N}{format.Extension}";
        await _storage.SaveAsync(key, content, format.ContentType);

        var media = new MediaEntity
        {
            EventId = eventId,
            UploaderUserId = userId,
            Type = MediaType.Photo,
            Purpose = MediaPurpose.WebsiteAsset,
            StorageKey = key,
            ContentType = format.ContentType,
            SizeBytes = length,
        };
        _context.Media.Add(media);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Don't leave a file nothing points to
            await TryDeleteAsync(key);
            throw;
        }

        return media.ToMediaDto(_storage);
    }

    // Helpers
    private static async Task<PhotoFormat?> DetectPhotoAsync(Stream content)
    {
        var header = new byte[12];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        content.Position = 0;

        var bytes = header.AsSpan(0, read);
        if (bytes.StartsWith((byte[])[0xFF, 0xD8, 0xFF]))
            return new PhotoFormat("image/jpeg", ".jpg");
        if (bytes.StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return new PhotoFormat("image/png", ".png");
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))
            return new PhotoFormat("image/gif", ".gif");
        if (read >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return new PhotoFormat("image/webp", ".webp");
        return null;
    }

    private async Task TryDeleteAsync(string key)
    {
        try
        {
            await _storage.DeleteAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not delete the stored file {StorageKey} after a failed save.", key);
        }
    }

    private static AppValidationException FileError(string message) =>
        new(message, new Dictionary<string, List<string>> { ["file"] = [message] });
}
