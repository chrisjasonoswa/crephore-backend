using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.EventGroups.Dtos;

public class CreateEventGroupRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // The user's events to put in the group (moved out of any other group); null or [] for none
    public List<int>? EventIds { get; set; }
}
