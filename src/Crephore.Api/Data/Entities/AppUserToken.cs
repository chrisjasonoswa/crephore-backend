using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Crephore.Api.Data.IEntities;

namespace Crephore.Api.Data.Entities;

/*
 * A refresh token (as in zao-pos-backend). Only its SHA-256 hash is stored.
 * Each refresh rotates it: the old token is revoked and points to its replacement (ReplacedByTokenHash),
 * so a reused old token ends the login (after a short grace period). All tokens of one login share its SessionId.
 */
[Table("app_user_tokens")]
public class AppUserToken : IHasTimestamp
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string TokenHash { get; set; } = null!;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    // Expiration and SecurityStamp (the user's stamp when issued; a changed stamp rejects the refresh)
    [MaxLength(256)]
    public string SecurityStamp { get; set; } = null!;
    // At most RefreshTokenLifetime, and never past SessionExpiresAt
    public DateTimeOffset ExpiresAt { get; set; }
    // The login's hard limit (SessionMaxLifetime after sign-in), carried to every rotated token
    public DateTimeOffset SessionExpiresAt { get; set; }

    // Rotation tracking
    [MaxLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    // Revocation
    public DateTimeOffset? RevokedAt { get; set; }

    // Metadata
    [MaxLength(64)]
    public string SessionId { get; set; } = null!;
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
