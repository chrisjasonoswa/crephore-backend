namespace Crephore.Api.Features.AppUsers.Dtos;

public class AppUserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string Timezone { get; set; } = null!;
    public bool IsSuperAdmin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
