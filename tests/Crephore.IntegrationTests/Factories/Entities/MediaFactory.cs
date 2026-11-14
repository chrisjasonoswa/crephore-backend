using Crephore.Api.Common.Constants;
using Crephore.Api.Data;
using MediaEntity = Crephore.Api.Data.Entities.Media;

namespace Crephore.IntegrationTests.Factories.Entities;

// A photo uploaded for an event: only its row (no file is stored), enough for sections and venues to reference it.
// Its URL is CustomWebApplicationFactory.StoragePublicBaseUrl + "/" + StorageKey.
public class MediaFactory(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<MediaEntity> CreatePhotoAsync(int eventId)
    {
        var media = new MediaEntity
        {
            EventId = eventId,
            Type = MediaType.Photo,
            Purpose = MediaPurpose.WebsiteAsset,
            StorageKey = $"events/{eventId}/{Guid.NewGuid():N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
        };

        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        return media;
    }
}
