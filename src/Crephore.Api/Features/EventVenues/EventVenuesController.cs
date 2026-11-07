using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.EventVenues.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.EventVenues;

// An event's venues (where it happens), for users with access to the event.
[Route(ApiRoutes.EventVenues.Route)]
[ApiController]
[Authorize]
public class EventVenuesController(
    IEventVenuesService eventVenuesService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IEventVenuesService _eventVenuesService = eventVenuesService;
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpGet]
    public async Task<IActionResult> Get(int eventId)
    {
        var venues = await _eventVenuesService.GetAsync(eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<List<EventVenueDto>>.Ok(venues));
    }

    [HttpPut]
    public async Task<IActionResult> Update(int eventId, [FromBody] UpdateEventVenuesRequestDto request)
    {
        var venues = await _eventVenuesService.UpdateAsync(eventId, _currentUser.GetUserId(), request);
        return Ok(ApiResponse<List<EventVenueDto>>.Ok(venues));
    }
}
