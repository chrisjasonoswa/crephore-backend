using Crephore.Api.Data.Entities;
using Crephore.Api.Features.AppUsers.Dtos;

namespace Crephore.Api.Features.AppUsers;

public static class AppUsersMapper
{
    public static AppUserDto ToAppUserDto(this AppUser user)
    {
        return new AppUserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Timezone = user.Timezone,
            IsSuperAdmin = user.IsSuperAdmin,
            CreatedAt = user.CreatedAt,
        };
    }
}
