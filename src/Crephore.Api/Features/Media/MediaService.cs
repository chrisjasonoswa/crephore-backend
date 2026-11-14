using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Common.Storage;
using Crephore.Api.Data;
using Crephore.Api.Features.Media.Dtos;
using Microsoft.EntityFrameworkCore;
using MediaEntity = Crephore.Api.Data.Entities.Media;

namespace Crephore.Api.Features.Media;

public interface IMediaService
{
    Task<MediaDto> CreatePhotoAsync(int eventId, int userId, Stream content, long length);
    Task<Dictionary<int, string>> GetUrlsAsync(int eventId, IEnumerable<int> mediaIds);
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
    // request's content type, so only real JPEG, PNG, WebP, AVIF and GIF images are stored (no SVG: it can run scripts).
    public async Task<MediaDto> CreatePhotoAsync(int eventId, int userId, Stream content, long length)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        if (length == 0)
            throw FileError("The photo is empty.");
        if (length > PhotoMaxBytes)
            throw FileError("The photo must be 10 MB or smaller.");

        var format = await DetectPhotoAsync(content)
            ?? throw FileError("The photo must be a JPG, PNG, WebP, AVIF or GIF image.");

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

    // Where browsers load each of the event's media among `mediaIds` from, by id, for photos stored as references
    // (MediaReferences). Ids that aren't this event's media are left out, so callers also use it to check references.
    // Callers check access to the event first.
    public async Task<Dictionary<int, string>> GetUrlsAsync(int eventId, IEnumerable<int> mediaIds)
    {
        var ids = mediaIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var keys = await _context.Media
            .AsNoTracking()
            .Where(m => m.EventId == eventId && ids.Contains(m.Id))
            .Select(m => new { m.Id, m.StorageKey })
            .ToListAsync();

        return keys.ToDictionary(m => m.Id, m => _storage.GetUrl(m.StorageKey));
    }

    // Helpers
    private static async Task<PhotoFormat?> DetectPhotoAsync(Stream content)
    {
        // 64 bytes: enough for an AVIF file's `ftyp` box and its list of brands
        var header = new byte[64];
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
        if (IsAvif(bytes))
            return new PhotoFormat("image/avif", ".avif");
        return null;
    }

    // An AVIF file opens with an ISO media `ftyp` box (its size, "ftyp", the main brand, a version, then more brands);
    // it's AVIF when "avif" (a still image) or "avis" (an image sequence) is among the brands. Some encoders give "mif1"
    // as the main brand and list "avif" after it, so every brand in the box is checked. HEIC files (brand "heic") aren't
    // accepted: most browsers can't show them
    private static bool IsAvif(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16 || !bytes[4..8].SequenceEqual("ftyp"u8))
            return false;
        var boxSize = (int)Math.Min(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes), (uint)bytes.Length);
        bool IsAvifBrand(ReadOnlySpan<byte> brand) => brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);
        if (IsAvifBrand(bytes[8..12]))
            return true;
        // The other brands follow the version (bytes 12 to 15), four bytes each
        for (var offset = 16; offset + 4 <= boxSize; offset += 4)
        {
            if (IsAvifBrand(bytes[offset..(offset + 4)]))
                return true;
        }
        return false;
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
