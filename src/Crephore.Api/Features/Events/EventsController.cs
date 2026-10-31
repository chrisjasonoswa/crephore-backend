using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.Events.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.Events;

// The signed-in user's events; every action is scoped to the current user.
[Route(ApiRoutes.Events.Route)]
[ApiController]
[Authorize]
public class EventsController(
    IEventsService eventsService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IEventsService _eventsService = eventsService;
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListEventsRequestDto request)
    {
        var events = await _eventsService.ListAsync(_currentUser.GetUserId(), request);
        return Ok(ApiResponse<ApiPaginationResponseDto<EventDto>>.Ok(events));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var @event = await _eventsService.GetAsync(id, _currentUser.GetUserId());
        return Ok(ApiResponse<EventDto>.Ok(@event));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequestDto request)
    {
        var @event = await _eventsService.CreateAsync(_currentUser.GetUserId(), request);
        return Ok(ApiResponse<EventDto>.Ok(@event));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequestDto request)
    {
        var @event = await _eventsService.UpdateAsync(id, _currentUser.GetUserId(), request);
        return Ok(ApiResponse<EventDto>.Ok(@event));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _eventsService.DeleteAsync(id, _currentUser.GetUserId());
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
