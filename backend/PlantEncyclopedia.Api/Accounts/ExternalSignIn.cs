using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Api.Accounts;

/// <summary>Sign-in providers (D-070, Phase 6a-2): Google and GitHub. Each is on only when its keys are configured.</summary>
public sealed class ExternalProviderOptions
{
    public ProviderKeys Google { get; set; } = new();
    public ProviderKeys GitHub { get; set; } = new();
}

/// <summary>An OAuth app's client ID and secret. User Secrets only.</summary>
public sealed class ProviderKeys
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>What a provider told us about the person who signed in.</summary>
/// <param name="EmailVerified">The provider confirms the person owns the address (only Google says so; see <see cref="ExternalSignIn"/>).</param>
public sealed record ExternalIdentity(string Provider, string ProviderKey, string? Email, bool EmailVerified, string? Name)
{
    /// <summary>The claim Google's user info "email_verified" field is mapped to (v3 userinfo endpoint).</summary>
    public const string EmailVerifiedClaim = "email_verified";

    public static ExternalIdentity From(ExternalLoginInfo info)
    {
        var email = info.Principal.FindFirstValue(ClaimTypes.Email)?.Trim();
        var verified = info.LoginProvider == ExternalSignIn.Google
            && string.Equals(info.Principal.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);
        return new ExternalIdentity(
            info.LoginProvider,
            info.ProviderKey,
            string.IsNullOrEmpty(email) ? null : email,
            verified,
            info.Principal.FindFirstValue(ClaimTypes.Name));
    }
}

public enum ExternalSignInStatus
{
    /// <summary>The provider was already linked to an account.</summary>
    SignedIn,

    /// <summary>A new account was created for this provider sign-in.</summary>
    Created,

    /// <summary>The provider was linked to the existing account with the same, provider-verified email.</summary>
    Linked,

    /// <summary>An account already uses the email, and the provider does not confirm the person owns it.</summary>
    EmailInUse,

    /// <summary>The provider shared no email address; every account needs one (unique emails, confirmation).</summary>
    EmailRequired,

    Locked,
    Failed
}

/// <summary>
/// Signs a person in from a provider (D-070). Matching is deliberately strict, because an unverified email would let
/// anyone take over an account by registering that address at a provider:
/// <list type="bullet">
/// <item>A provider already linked to an account signs into it.</item>
/// <item>A new provider joins an existing account only when the emails match and the provider verified the address
/// (Google reports this; the GitHub handler does not check it).</item>
/// <item>Otherwise, an email already in use is refused with a message; a new email gets a new account. A provider that
/// shares no email address is refused, because every account needs one.</item>
/// </list>
/// </summary>
public sealed class ExternalSignIn(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
{
    public const string Google = "Google";
    public const string GitHub = "GitHub";

    public async Task<(ExternalSignInStatus Status, ApplicationUser? User)> SignInAsync(ExternalIdentity identity)
    {
        var existing = await signIn.ExternalLoginSignInAsync(identity.Provider, identity.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (existing.Succeeded) return (ExternalSignInStatus.SignedIn, await users.FindByLoginAsync(identity.Provider, identity.ProviderKey));
        if (existing.IsLockedOut) return (ExternalSignInStatus.Locked, null);

        if (identity.Email is null) return (ExternalSignInStatus.EmailRequired, null);

        var login = new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider);
        var sameEmail = await users.FindByEmailAsync(identity.Email);
        if (sameEmail is not null)
        {
            if (!identity.EmailVerified) return (ExternalSignInStatus.EmailInUse, null);
            if (await users.IsLockedOutAsync(sameEmail)) return (ExternalSignInStatus.Locked, null);
            if (!(await users.AddLoginAsync(sameEmail, login)).Succeeded) return (ExternalSignInStatus.Failed, null);
            if (!sameEmail.EmailConfirmed)
            {
                sameEmail.EmailConfirmed = true; // the provider has just proved the person owns the address
                await users.UpdateAsync(sameEmail);
            }

            await signIn.SignInAsync(sameEmail, isPersistent: true);
            return (ExternalSignInStatus.Linked, sameEmail);
        }

        var user = new ApplicationUser
        {
            UserName = identity.Email,
            Email = identity.Email,
            EmailConfirmed = identity.EmailVerified,
            DisplayName = DisplayName(identity)
        };
        if (!(await users.CreateAsync(user)).Succeeded) return (ExternalSignInStatus.Failed, null);
        if (!(await users.AddLoginAsync(user, login)).Succeeded)
        {
            await users.DeleteAsync(user);
            return (ExternalSignInStatus.Failed, null);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return (ExternalSignInStatus.Created, user);
    }

    // The provider's name for the person, else the part of the email before "@", within the 50-character limit.
    private static string DisplayName(ExternalIdentity identity)
    {
        var name = identity.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Any(char.IsControl)) name = identity.Email!.Split('@')[0];
        if (string.IsNullOrEmpty(name)) name = "Plant lover";
        return name.Length > ApplicationUser.DisplayNameMaxLength ? name[..ApplicationUser.DisplayNameMaxLength] : name;
    }
}
