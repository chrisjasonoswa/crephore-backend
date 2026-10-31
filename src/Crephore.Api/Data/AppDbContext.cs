using System.Text.Json;
using Crephore.Api.Common.Constants;
using Crephore.Api.Data.Entities;
using Crephore.Api.Data.IEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Crephore.Api.Data;

// IdentityUserContext = users-only Identity schema (no roles tables).
// Switch to IdentityDbContext if role-based permissions are needed.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, int>(options)
{
    /* DbSets for entities */
    // Users is inherited from IdentityUserContext
    public DbSet<AppUserToken> AppUserTokens { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventUser> EventUsers { get; set; }
    public DbSet<EventGroup> EventGroups { get; set; }
    public DbSet<EventWebsite> EventWebsites { get; set; }
    public DbSet<WebsiteSection> WebsiteSections { get; set; }
    public DbSet<Media> Media { get; set; }


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
        // Identity's own token table (unused for now); `app_user_tokens` holds refresh tokens (AppUserToken)
        builder.Entity<IdentityUserToken<int>>().ToTable("app_user_identity_tokens");

        // Refresh tokens
        builder.Entity<AppUserToken>(entity =>
        {
            // Refresh looks tokens up by hash
            entity.HasIndex(t => t.TokenHash)
                .IsUnique();

            // Logout revokes a session's tokens
            entity.HasIndex(t => new { t.UserId, t.SessionId });

            entity.HasOne(t => t.User)
                .WithMany(u => u.Tokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Events
        builder.Entity<Event>(entity =>
        {
            // Stored as its name (e.g. "Wedding") so new enum values never shift stored data
            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(e => e.Subdomain)
                .IsUnique()
                .HasDatabaseName(UniqueIndexes.EventSubdomain);

            // JSON array of strings; existing rows get [] (MySQL only allows expression defaults on json)
            entity.PrimitiveCollection(e => e.Timeline)
                .HasColumnType("json")
                .HasDefaultValueSql("(JSON_ARRAY())");
        });

        // Event users (a user's access to an event; deleted with the event or the user)
        builder.Entity<EventUser>(entity =>
        {
            // JSON array of names, e.g. ["Owner"]
            entity.PrimitiveCollection(eu => eu.Access)
                .HasColumnType("json")
                .ElementType()
                .HasConversion<string>();

            entity.HasOne(eu => eu.User)
                .WithMany(u => u.EventUsers)
                .HasForeignKey(eu => eu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(eu => eu.Event)
                .WithMany(e => e.EventUsers)
                .HasForeignKey(eu => eu.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a group ungroups its events
            entity.HasOne(eu => eu.EventGroup)
                .WithMany(g => g.EventUsers)
                .HasForeignKey(eu => eu.EventGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Event groups (names unique per owner)
        builder.Entity<EventGroup>(entity =>
        {
            entity.HasIndex(g => new { g.OwnerId, g.Name })
                .IsUnique()
                .HasDatabaseName(UniqueIndexes.EventGroupOwnerName);

            entity.HasOne(g => g.Owner)
                .WithMany(u => u.EventGroups)
                .HasForeignKey(g => g.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Event websites (at most one per event; deleted with it)
        builder.Entity<EventWebsite>(entity =>
        {
            entity.HasIndex(w => w.EventId)
                .IsUnique()
                .HasDatabaseName(UniqueIndexes.EventWebsiteEvent);

            entity.Property(w => w.PublishedContent)
                .HasColumnType("json");

            entity.HasOne(w => w.Event)
                .WithOne(e => e.Website)
                .HasForeignKey<EventWebsite>(w => w.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Website sections (deleted with their website)
        builder.Entity<WebsiteSection>(entity =>
        {
            // JSON objects whose fields the API doesn't know (see WebsiteSection)
            entity.Property(s => s.Options)
                .HasColumnType("json")
                .HasConversion(
                    options => JsonSerializer.Serialize(options, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null) ?? new(),
                    new ValueComparer<Dictionary<string, string>>(
                        (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                        options => options.Aggregate(0, (hash, option) => HashCode.Combine(hash, option.Key, option.Value)),
                        options => new Dictionary<string, string>(options)));
            entity.Property(s => s.Content)
                .HasColumnType("json");

            // Stored as its name; existing sections are Always
            entity.Property(s => s.Visibility)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(SectionVisibility.Always);

            // The page is read in order
            entity.HasIndex(s => new { s.EventWebsiteId, s.SortOrder });

            entity.HasOne(s => s.EventWebsite)
                .WithMany(w => w.Sections)
                .HasForeignKey(s => s.EventWebsiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Uploaded files (deleted with their event; the uploader is cleared when their account is deleted)
        builder.Entity<Media>(entity =>
        {
            entity.Property(m => m.Type)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(m => m.Purpose)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(m => m.StorageKey)
                .IsUnique();

            entity.HasOne(m => m.Event)
                .WithMany(e => e.Media)
                .HasForeignKey(m => m.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.UploaderUser)
                .WithMany()
                .HasForeignKey(m => m.UploaderUserId)
                .OnDelete(DeleteBehavior.SetNull);
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
