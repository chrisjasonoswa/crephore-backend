using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Features.EventGroups.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.EventGroups;

// The signed-in user's event groups; every action is scoped to the current user.
[Route(ApiRoutes.EventGroups.Route)]
[ApiController]
[Authorize]
public class EventGroupsController(
    IEventGroupsService eventGroupsService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IEventGroupsService _eventGroupsService = eventGroupsService;
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListEventGroupsRequestDto request)
    {
        var groups = await _eventGroupsService.ListAsync(_currentUser.GetUserId(), request);
        return Ok(ApiResponse<ApiPaginationResponseDto<EventGroupDto>>.Ok(groups));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventGroupRequestDto request)
    {
        var group = await _eventGroupsService.CreateAsync(_currentUser.GetUserId(), request);
        return Ok(ApiResponse<EventGroupDto>.Ok(group));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventGroupRequestDto request)
    {
        var group = await _eventGroupsService.UpdateAsync(id, _currentUser.GetUserId(), request);
        return Ok(ApiResponse<EventGroupDto>.Ok(group));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _eventGroupsService.DeleteAsync(id, _currentUser.GetUserId());
        return Ok(ApiResponse<object?>.Ok(null));
    }

    [HttpPost("{id:int}/" + ApiRoutes.EventGroups.Events.RelativeRoute + "/{eventId:int}")]
    public async Task<IActionResult> AddEvent(int id, int eventId)
    {
        var group = await _eventGroupsService.AddEventAsync(id, eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<EventGroupDto>.Ok(group));
    }

    [HttpDelete("{id:int}/" + ApiRoutes.EventGroups.Events.RelativeRoute + "/{eventId:int}")]
    public async Task<IActionResult> RemoveEvent(int id, int eventId)
    {
        var group = await _eventGroupsService.RemoveEventAsync(id, eventId, _currentUser.GetUserId());
        return Ok(ApiResponse<EventGroupDto>.Ok(group));
    }
}
