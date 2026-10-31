namespace Crephore.Api.Features.Auth.Dtos;

// Web clients send the refresh token as a cookie; API clients send it in the body
public class RefreshRequestDto
{
    public string? RefreshToken { get; set; }
}

public class RefreshResponseDto : ITokenSessionDto
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required string SessionId { get; set; }
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
    public DateTimeOffset RefreshTokenExpiresAt { get; set; }
}
