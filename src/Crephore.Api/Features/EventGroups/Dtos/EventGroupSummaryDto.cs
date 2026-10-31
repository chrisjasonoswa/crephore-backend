namespace Crephore.Api.Features.EventGroups.Dtos;

// The group an event belongs to, as shown on the event
public class EventGroupSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
