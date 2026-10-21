using Microsoft.AspNetCore.Identity;

namespace PlantEncyclopedia.Infrastructure.Identity;

/// <summary>
/// A user account (D-070), stored by ASP.NET Core Identity in the users table. Kept minimal: email, display name and
/// timestamps; passwords only as Identity's salted hash; sign-in providers in user_logins.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 50;

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
