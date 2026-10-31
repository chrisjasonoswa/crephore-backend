namespace Crephore.Api.Features.Auth.Dtos;

// What login and refresh return (and set as cookies for web clients)
public interface ITokenSessionDto
{
    string AccessToken { get; }
    string RefreshToken { get; }
    string SessionId { get; }
    DateTimeOffset AccessTokenExpiresAt { get; }
    DateTimeOffset RefreshTokenExpiresAt { get; }
}
