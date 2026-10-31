using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.EventWebsites.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.EventWebsites;

// An event's website (one per event), for users with access to the event.
[Route(ApiRoutes.EventWebsites.Route)]
[ApiController]
[Authorize]
public class EventWebsitesController(
    IEventWebsitesService eventWebsitesService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IEventWebsitesService _eventWebsitesService = eventWebsitesService;
    private readonly ICurrentUser _currentUser = currentUser;

    // `data` is null until the host saves a website
    [HttpGet]
    public async Task<IActionResult> Get(int eventId)
    {
        var website = await _eventWebsitesService.GetAsync(eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<EventWebsiteDto?>.Ok(website));
    }

    [HttpPut]
    public async Task<IActionResult> Update(int eventId, [FromBody] UpdateEventWebsiteRequestDto request)
    {
        var website = await _eventWebsitesService.UpdateAsync(eventId, _currentUser.GetUserId(), request);
        return Ok(ApiResponse<EventWebsiteDto>.Ok(website));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int eventId)
    {
        await _eventWebsitesService.DeleteAsync(eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<object?>.Ok(null));
    }

    [HttpPost(ApiRoutes.EventWebsites.Publish.RelativeRoute)]
    public async Task<IActionResult> Publish(int eventId)
    {
        var website = await _eventWebsitesService.PublishAsync(eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<EventWebsiteDto>.Ok(website));
    }

    [HttpPost(ApiRoutes.EventWebsites.Unpublish.RelativeRoute)]
    public async Task<IActionResult> Unpublish(int eventId)
    {
        var website = await _eventWebsitesService.UnpublishAsync(eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<EventWebsiteDto>.Ok(website));
    }
}
