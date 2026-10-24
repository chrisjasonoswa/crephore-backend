using System.ComponentModel.DataAnnotations;
using Crephore.Api.Data.IEntities;
using Microsoft.AspNetCore.Identity;

namespace Crephore.Api.Data.Entities;

//[Table("app_users")] - Does not work since IdentityUserContext maps the user entity to "AspNetUsers" via the fluent API,
//which overrides data annotations. The table name is set in AppDbContext.OnModelCreating instead.
public class AppUser : IdentityUser<int>, IHasCreatedAt
{
    // Personal Info
    [MaxLength(100)]
    public string FirstName { get; set; } = null!;
    [MaxLength(100)]
    public string LastName { get; set; } = null!;
    /* The following are inherited from IdentityUser:cd 
     *  - Email / NormalizedEmail (unique, see AppDbContext)
     *  - PhoneNumber
     *  - PasswordHash
     *  - UserName (generated, not user-facing)
     *  - SecurityStamp, lockout and confirmation fields
     */

    // Security Info
    public bool IsSuperAdmin { get; set; } = false;


    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }

    /* Related Properties */
    // TODO: Add `ICollection<Event> Events` once the Event entity exists.
    public ICollection<AppUserSession> Sessions { get; set; } = new List<AppUserSession>();
}
