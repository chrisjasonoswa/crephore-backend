using System.ComponentModel.DataAnnotations;
using Crephore.Api.Features.AppUsers.Dtos;

namespace Crephore.Api.Features.Auth.Dtos;

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto : ITokenSessionDto
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required string SessionId { get; set; }
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
    public DateTimeOffset RefreshTokenExpiresAt { get; set; }
    public required AppUserDto User { get; set; }
}
