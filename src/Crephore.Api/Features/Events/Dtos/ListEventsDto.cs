using Crephore.Api.Common.Api;

namespace Crephore.Api.Features.Events.Dtos;

public class ListEventsRequestDto : ApiPaginationRequestDto
{
    // Only events in this group
    public int? EventGroupId { get; set; }

    // SearchKey matches the name or the subdomain.
    // Supported sort fields: id, name, type, startsAt, createdAt, subdomain (default: startsAt:asc)
}
