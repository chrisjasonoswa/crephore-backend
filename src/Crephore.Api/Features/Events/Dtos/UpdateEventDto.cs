namespace Crephore.Api.Features.Events.Dtos;

// Same fields and rules as create, without the group: replaces them all and leaves the user's group as it is
public class UpdateEventRequestDto : EventRequestDto;
