using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

/*
 * A login session. Every access token carries the SessionId ("sid" claim),
 * and the JWT bearer handler rejects tokens whose session is revoked or expired.
 * This is what makes logout invalidate an access token immediately.
 */
[Table("app_user_sessions")]
public class AppUserSession : IHasTimestamp
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(64)]
    public string SessionId { get; set; } = null!;

    // Expiration and Revocation
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Metadata
    [MaxLength(512)]
    public string? UserAgent { get; set; }
    [MaxLength(64)]
    public string? CreatedByIp { get; set; }
    [MaxLength(64)]
    public string? RevokedByIp { get; set; }

    // Timestamp
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Helper properties
    [NotMapped]
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsActive => RevokedAt == null && !IsExpired;
}
