using Crephore.Api.Data.Entities;
using Crephore.Api.Data.IEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Data;

// IdentityUserContext = users-only Identity schema (no roles tables).
// Switch to IdentityDbContext if role-based permissions are needed.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, int>(options)
{
    /* DbSets for entities */
    // Users is inherited from IdentityUserContext
    public DbSet<AppUserSession> AppUserSessions { get; set; }


    /* Model configuration */
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Custom DB names for identity tables */
        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");

            // Identity indexes NormalizedEmail but not uniquely; enforce unique emails at the DB level too.
            entity.HasIndex(u => u.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
        });
        builder.Entity<IdentityUserClaim<int>>().ToTable("app_user_claims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("app_user_logins");
        builder.Entity<IdentityUserToken<int>>().ToTable("app_user_tokens");

        // User sessions
        builder.Entity<AppUserSession>(entity =>
        {
            entity.HasIndex(s => s.SessionId)
                .IsUnique();

            // Lookup path used by session validation and logout
            entity.HasIndex(s => new { s.UserId, s.SessionId });

            entity.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /* Timestamp */
    private void SetTimestamp()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is not IHasCreatedAt entity)
                continue;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
                if (entity is IHasTimestamp timestamped)
                    timestamped.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IHasCreatedAt.CreatedAt)).IsModified = false;
                if (entity is IHasTimestamp timestamped)
                    timestamped.UpdatedAt = now;
            }
        }
    }

    public override int SaveChanges()
    {
        SetTimestamp();

        return base.SaveChanges();
    }


    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        SetTimestamp();

        return base.SaveChangesAsync(cancellationToken);
    }
}
