using Crephore.Api.Common.Storage;
using Crephore.Api.Features.Media.Dtos;
using MediaEntity = Crephore.Api.Data.Entities.Media;

namespace Crephore.Api.Features.Media;

public static class MediaMapper
{
    // The URL comes from the storage, so it follows the storage's address
    public static MediaDto ToMediaDto(this MediaEntity media, IFileStorage storage)
    {
        return new MediaDto
        {
            Id = media.Id,
            Type = media.Type,
            Purpose = media.Purpose,
            Url = storage.GetUrl(media.StorageKey),
            ContentType = media.ContentType,
            SizeBytes = media.SizeBytes,
            CreatedAt = media.CreatedAt,
        };
    }
}
