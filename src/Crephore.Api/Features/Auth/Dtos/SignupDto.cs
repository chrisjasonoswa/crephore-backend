using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.Auth.Dtos;

public class SignupRequestDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Phone]
    [MaxLength(32)]
    public string? PhoneNumber { get; set; }
}
