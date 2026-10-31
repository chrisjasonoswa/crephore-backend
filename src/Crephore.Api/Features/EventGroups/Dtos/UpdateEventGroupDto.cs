using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.EventGroups.Dtos;

public class UpdateEventGroupRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // The group's events afterwards: listed events are added (moved out of any other group), the rest removed.
    // Null keeps the current events, so a rename alone never empties a group.
    public List<int>? EventIds { get; set; }
}
