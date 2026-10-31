using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.Media.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.Media;

// Files uploaded for an event. For now: photos for its website.
[Route(ApiRoutes.Media.Route)]
[ApiController]
[Authorize]
public class MediaController(
    IMediaService mediaService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IMediaService _mediaService = mediaService;
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpPost]
    [RequestSizeLimit(MediaService.RequestMaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.RequestMaxBytes)]
    public async Task<IActionResult> Create(int eventId, [FromForm] CreateMediaRequestDto request)
    {
        await using var content = request.File.OpenReadStream();
        var media = await _mediaService.CreatePhotoAsync(eventId, _currentUser.GetUserId(), content, request.File.Length);
        return Ok(ApiResponse<MediaDto>.Ok(media));
    }
}
