using Crephore.Api.Common.Api;

namespace Crephore.Api.Features.EventGroups.Dtos;

public class ListEventGroupsRequestDto : ApiPaginationRequestDto
{
    // SearchKey matches the name.
    // Supported sort fields: id, name, eventCount, createdAt (default: name:asc)
}
