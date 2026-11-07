using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.EventPrograms.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.EventPrograms;

// An event's program (its schedule), for users with access to the event.
[Route(ApiRoutes.EventPrograms.Route)]
[ApiController]
[Authorize]
public class EventProgramsController(
    IEventProgramsService eventProgramsService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IEventProgramsService _eventProgramsService = eventProgramsService;
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpGet]
    public async Task<IActionResult> Get(int eventId)
    {
        var program = await _eventProgramsService.GetAsync(eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<EventProgramDto>.Ok(program));
    }

    [HttpPut]
    public async Task<IActionResult> Update(int eventId, [FromBody] UpdateEventProgramRequestDto request)
    {
        var program = await _eventProgramsService.UpdateAsync(eventId, _currentUser.GetUserId(), request);
        return Ok(ApiResponse<EventProgramDto>.Ok(program));
    }
}
