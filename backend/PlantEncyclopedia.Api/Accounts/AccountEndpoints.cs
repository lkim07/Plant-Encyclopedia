using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Api.Accounts;

/// <summary>A signed-in user, as the API shows it (API_SPEC §6).</summary>
public sealed record UserResponse(Guid Id, string DisplayName, string? Email, bool EmailConfirmed)
{
    public static UserResponse From(ApplicationUser user) => new(user.Id, user.DisplayName, user.Email, user.EmailConfirmed);
}

/// <summary>
/// Email + password accounts (D-070, API_SPEC §6). Every endpoint that changes state requires the CSRF token and is
/// rate-limited per client. Responses never reveal whether an address has an account, except registration
/// (an address that is taken cannot be registered again).
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", GetMeAsync);

        var auth = app.MapGroup("/api/auth").RequireRateLimiting(AccountSetup.RateLimitPolicy);
        auth.MapPost("/register", RegisterAsync).RequireCsrfToken();
        auth.MapPost("/login", LoginAsync).RequireCsrfToken();
        auth.MapPost("/logout", LogoutAsync).RequireCsrfToken();
        auth.MapPost("/forgot-password", ForgotPasswordAsync).RequireCsrfToken();
        auth.MapPost("/reset-password", ResetPasswordAsync).RequireCsrfToken();
        auth.MapPost("/resend-confirmation", ResendConfirmationAsync).RequireCsrfToken().RequireAuthorization();
        auth.MapGet("/confirm-email", ConfirmEmailAsync);
        return app;
    }

    // GET /api/me: the signed-in user, or 401. Always issues a fresh CSRF token for the browser.
    private static async Task<IResult> GetMeAsync(HttpContext context, UserManager<ApplicationUser> users)
    {
        AccountSetup.IssueXsrfToken(context);
        var user = context.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(context.User) : null;
        return user is null
            ? Results.Json(new { error = new { code = "UNAUTHENTICATED", message = "Sign in to continue." } }, statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(new { data = UserResponse.From(user) });
    }

    // POST /api/auth/register {email, password, displayName}: creates the account, signs it in, and emails a
    // confirmation link. Confirmation is needed only to save Favorites.
    private static async Task<IResult> RegisterAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IAccountEmailSender emails,
        AccountOptions options,
        CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<RegisterRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;

        var details = new Dictionary<string, string>();
        var (email, emailError) = AccountInput.Email(body!.Email);
        var (name, nameError) = AccountInput.DisplayName(body.DisplayName);
        var passwordError = AccountInput.NewPassword(body.Password);
        if (emailError is not null) details["email"] = emailError;
        if (nameError is not null) details["displayName"] = nameError;
        if (passwordError is not null) details["password"] = passwordError;
        if (details.Count > 0) return ApiErrors.Validation(details);

        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name! };
        var created = await users.CreateAsync(user, body.Password!);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return Results.Conflict(new { error = new { code = "EMAIL_TAKEN", message = "An account with this email address already exists." } });
            }

            return ApiErrors.Validation("password", created.Errors.First().Description);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        await SendConfirmationAsync(user, users, emails, options, cancellationToken);
        await SignedInAsAsync(context, signIn, user);
        return Results.Created("/api/me", new { data = UserResponse.From(user) });
    }

    // POST /api/auth/login {email, password}: 5 failed attempts lock the account for 15 minutes.
    private static async Task<IResult> LoginAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<LoginRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;

        var (email, emailError) = AccountInput.Email(body!.Email);
        if (emailError is not null) return ApiErrors.Validation("email", emailError);
        if (string.IsNullOrEmpty(body.Password) || body.Password.Length > AccountSetup.MaxPasswordLength)
        {
            return ApiErrors.Validation("password", "Enter your password.");
        }

        var user = await users.FindByEmailAsync(email!);
        if (user is null) return InvalidCredentials();

        var result = await signIn.PasswordSignInAsync(user, body.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Results.Json(
                new { error = new { code = "ACCOUNT_LOCKED", message = "Too many failed sign-in attempts. Try again in 15 minutes." } },
                statusCode: StatusCodes.Status423Locked);
        }

        if (!result.Succeeded) return InvalidCredentials();

        await SignedInAsAsync(context, signIn, user);
        return Results.Ok(new { data = UserResponse.From(user) });
    }

    // POST /api/auth/logout
    private static async Task<IResult> LogoutAsync(HttpContext context, SignInManager<ApplicationUser> signIn)
    {
        await signIn.SignOutAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        AccountSetup.IssueXsrfToken(context);
        return Results.NoContent();
    }

    // POST /api/auth/forgot-password {email}: always 202, whether or not the address has an account.
    private static async Task<IResult> ForgotPasswordAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        IAccountEmailSender emails,
        AccountOptions options,
        CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<ForgotPasswordRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;

        var (email, emailError) = AccountInput.Email(body!.Email);
        if (emailError is not null) return ApiErrors.Validation("email", emailError);

        if (await users.FindByEmailAsync(email!) is { } user)
        {
            var token = Encode(await users.GeneratePasswordResetTokenAsync(user));
            var link = new Uri(BaseUri(options), $"reset-password?email={Uri.EscapeDataString(email!)}&token={token}");
            await emails.SendPasswordResetAsync(user.Email!, link, cancellationToken);
        }

        return Results.Accepted(value: new { data = new { status = "sent" } });
    }

    // POST /api/auth/reset-password {email, token, password}
    private static async Task<IResult> ResetPasswordAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var (body, bodyError) = await AccountInput.ReadJsonAsync<ResetPasswordRequest>(context.Request, cancellationToken);
        if (bodyError is not null) return bodyError;

        var (email, emailError) = AccountInput.Email(body!.Email);
        var passwordError = AccountInput.NewPassword(body.Password);
        if (passwordError is not null) return ApiErrors.Validation("password", passwordError);

        var user = emailError is null ? await users.FindByEmailAsync(email!) : null;
        var token = Decode(body.Token);
        if (user is null || token is null) return InvalidLink("INVALID_RESET_LINK", "This reset link is invalid or has expired. Ask for a new one.");

        var reset = await users.ResetPasswordAsync(user, token, body.Password!);
        if (reset.Succeeded)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
            return Results.NoContent();
        }

        return reset.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
            ? InvalidLink("INVALID_RESET_LINK", "This reset link is invalid or has expired. Ask for a new one.")
            : ApiErrors.Validation("password", reset.Errors.First().Description);
    }

    // POST /api/auth/resend-confirmation (signed in)
    private static async Task<IResult> ResendConfirmationAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        IAccountEmailSender emails,
        AccountOptions options,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        if (!user.EmailConfirmed && user.Email is not null)
        {
            await SendConfirmationAsync(user, users, emails, options, cancellationToken);
        }

        return Results.Accepted(value: new { data = new { status = "sent" } });
    }

    // GET /api/auth/confirm-email?userId=&token= (the link in the email): confirms and returns to the app.
    private static async Task<IResult> ConfirmEmailAsync(string? userId, string? token, UserManager<ApplicationUser> users, AccountOptions options)
    {
        var user = Guid.TryParse(userId, out _) ? await users.FindByIdAsync(userId!) : null;
        var decoded = Decode(token);
        var confirmed = user is not null && decoded is not null && (await users.ConfirmEmailAsync(user, decoded)).Succeeded;
        return Results.Redirect(new Uri(BaseUri(options), $"email-confirmed?status={(confirmed ? "ok" : "invalid")}").ToString());
    }

    private static async Task SendConfirmationAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> users,
        IAccountEmailSender emails,
        AccountOptions options,
        CancellationToken cancellationToken)
    {
        var token = Encode(await users.GenerateEmailConfirmationTokenAsync(user));
        var link = new Uri(BaseUri(options), $"api/auth/confirm-email?userId={user.Id}&token={token}");
        await emails.SendEmailConfirmationAsync(user.Email!, user.DisplayName, link, cancellationToken);
    }

    // The new session's user, so the fresh CSRF token is bound to them.
    private static async Task SignedInAsAsync(HttpContext context, SignInManager<ApplicationUser> signIn, ApplicationUser user)
    {
        context.User = await signIn.CreateUserPrincipalAsync(user);
        AccountSetup.IssueXsrfToken(context);
    }

    private static Uri BaseUri(AccountOptions options) => new(options.PublicBaseUrl!.TrimEnd('/') + "/");

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? Decode(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 2048) return null;
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IResult InvalidCredentials() =>
        Results.Json(
            new { error = new { code = "INVALID_CREDENTIALS", message = "The email address or password is incorrect." } },
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult InvalidLink(string code, string message) =>
        Results.BadRequest(new { error = new { code, message } });
}
