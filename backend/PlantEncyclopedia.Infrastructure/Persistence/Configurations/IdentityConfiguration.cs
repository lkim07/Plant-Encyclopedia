using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Identity's tables under this database's names (users, user_logins, user_claims, user_tokens). Column names follow
/// the snake-case convention like every other table. Roles are not used.
/// </summary>
internal static class IdentityConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.DisplayName).HasMaxLength(ApplicationUser.DisplayNameMaxLength);
            b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");

            // Phone numbers are never collected, so they are not stored at all.
            b.Ignore(u => u.PhoneNumber);
            b.Ignore(u => u.PhoneNumberConfirmed);

            // Identity checks uniqueness in code; the unique index also stops two simultaneous sign-ups with one email.
            // NULL emails (a provider that shares none) do not collide in PostgreSQL.
            b.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
            b.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ux_users_normalized_user_name");
        });
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
