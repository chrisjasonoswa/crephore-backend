using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.Media.Dtos;

// multipart/form-data with the photo in `file`
public class CreateMediaRequestDto
{
    [Required]
    public IFormFile File { get; set; } = null!;
}
