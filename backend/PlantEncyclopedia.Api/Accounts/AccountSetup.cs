using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using PlantEncyclopedia.Api.Search;
using PlantEncyclopedia.Infrastructure.Identity;
using PlantEncyclopedia.Infrastructure.Persistence;

namespace PlantEncyclopedia.Api.Accounts;

/// <summary>Configuration section Accounts.</summary>
public sealed class AccountOptions
{
    public const string SectionName = "Accounts";

    /// <summary>The site address used in email links, e.g. http://localhost:4200 (the Angular app, which proxies /api).</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Where the development email stand-in writes emails, relative to the content root.</summary>
    public string EmailFolder { get; set; } = "App_Data/emails";
}

/// <summary>
/// Accounts and sessions (D-070, SECURITY §4, §16): ASP.NET Core Identity with a session cookie, CSRF protection
/// for requests that change data, a rate limit for account endpoints, and the development email stand-in.
/// </summary>
public static class AccountSetup
{
    public const string SessionCookieName = "pe_session";
    public const string AntiforgeryCookieName = "pe_antiforgery";

    /// <summary>The readable cookie Angular copies into the X-XSRF-TOKEN header on requests that change data.</summary>
    public const string XsrfCookieName = "XSRF-TOKEN";
    public const string XsrfHeaderName = "X-XSRF-TOKEN";

    public const string RateLimitPolicy = "accounts";
    public const int RequestsPerMinute = 10;

    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(3);

    public static IServiceCollection AddAccounts(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(AccountOptions.SectionName).Get<AccountOptions>() ?? new AccountOptions();
        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicBase)
            || publicBase.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(publicBase.Query))
        {
            throw new InvalidOperationException($"{AccountOptions.SectionName}:PublicBaseUrl must be the site's absolute address.");
        }

        services.AddSingleton(options);

        services
            .AddIdentityCore<ApplicationUser>(identity =>
            {
                // At least 8 characters, no forced character classes (the developer's choice; NIST SP 800-63B).
                identity.Password.RequiredLength = MinPasswordLength;
                identity.Password.RequireDigit = false;
                identity.Password.RequireLowercase = false;
                identity.Password.RequireUppercase = false;
                identity.Password.RequireNonAlphanumeric = false;
                identity.Password.RequiredUniqueChars = 1;

                identity.Lockout.MaxFailedAccessAttempts = 5;
                identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                identity.Lockout.AllowedForNewUsers = true;

                identity.User.RequireUniqueEmail = true;
                identity.User.AllowedUserNameCharacters = string.Empty; // empty allows any: the user name is the email address

                // Signing in does not need a confirmed email; saving Favorites does (D-041, developer decision).
                identity.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<PlantEncyclopediaDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = LinkLifetime);

        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddIdentityCookies();
        AddProviders(authentication, services, configuration);
        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = SessionCookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Lax; // Lax so the session survives returning from a sign-in provider
            cookie.ExpireTimeSpan = SessionLifetime;
            cookie.SlidingExpiration = true;

            // An API answers 401/403 instead of redirecting to a login page.
            cookie.Events.OnRedirectToLogin = context => WriteErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                "UNAUTHENTICATED", "Sign in to continue.");
            cookie.Events.OnRedirectToAccessDenied = context => WriteErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                "FORBIDDEN", "You do not have access to this.");
        });
        services.AddAuthorization();

        services.AddAntiforgery(antiforgery =>
        {
            antiforgery.HeaderName = XsrfHeaderName;
            antiforgery.Cookie.Name = AntiforgeryCookieName;
            antiforgery.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            antiforgery.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.Configure<RateLimiterOptions>(limiter =>
            SearchProtection.AddPerClientPolicy(limiter, RateLimitPolicy, RequestsPerMinute));

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "No email service is configured. Account emails are written to files only in Development and Testing.");
        }

        services.AddSingleton<IAccountEmailSender>(sp => new FileAccountEmailSender(
            Path.GetFullPath(options.EmailFolder, environment.ContentRootPath),
            sp.GetRequiredService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// Google and GitHub sign-in (Phase 6a-2), each registered only when its keys are in configuration
    /// (Accounts:Providers:{Google|GitHub}:ClientId and ClientSecret, User Secrets). The provider's answer
    /// lands in Identity's short-lived external cookie; /api/auth/external/callback turns it into a session.
    /// Callbacks are /signin-google and /signin-github on the site's address.
    /// </summary>
    private static void AddProviders(AuthenticationBuilder authentication, IServiceCollection services, IConfiguration configuration)
    {
        var providers = configuration.GetSection("Accounts:Providers").Get<ExternalProviderOptions>() ?? new ExternalProviderOptions();
        services.AddSingleton(providers);
        services.AddScoped<ExternalSignIn>();

        if (providers.Google.IsConfigured)
        {
            authentication.AddGoogle(ExternalSignIn.Google, google =>
            {
                google.ClientId = providers.Google.ClientId!;
                google.ClientSecret = providers.Google.ClientSecret!;
                google.SignInScheme = IdentityConstants.ExternalScheme;
                google.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                google.ClaimActions.MapJsonKey(ExternalIdentity.EmailVerifiedClaim, "email_verified");
            });
        }

        if (providers.GitHub.IsConfigured)
        {
            authentication.AddGitHub(ExternalSignIn.GitHub, github =>
            {
                github.ClientId = providers.GitHub.ClientId!;
                github.ClientSecret = providers.GitHub.ClientSecret!;
                github.SignInScheme = IdentityConstants.ExternalScheme;
                github.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                github.Scope.Add("user:email"); // a private address is read from the emails endpoint (primary, not verification-checked)
            });
        }
    }

    /// <summary>
    /// The Google and GitHub return address must be exactly the one registered with each provider: /signin-google or
    /// /signin-github on the site's public address (Accounts:PublicBaseUrl). The provider handlers build it from the
    /// request, so for the sign-in start and the provider callbacks, a request for the public host takes the public
    /// address's scheme. In development the site is http://localhost:4200 while the dev proxy reaches the API over
    /// HTTPS (the cookies require it); in production both are HTTPS and nothing changes. Runs after the HTTPS
    /// redirection, so it never causes a redirect.
    /// </summary>
    public static IApplicationBuilder UseExternalSignInAddress(this IApplicationBuilder app)
    {
        var publicBase = new Uri(app.ApplicationServices.GetRequiredService<AccountOptions>().PublicBaseUrl!);
        return app.Use((context, next) =>
        {
            var path = context.Request.Path;
            var isSignInRoute = path.StartsWithSegments("/signin-google")
                || path.StartsWithSegments("/signin-github")
                || (path.StartsWithSegments("/api/auth/external") && !path.StartsWithSegments("/api/auth/external/callback"));
            if (isSignInRoute && string.Equals(context.Request.Host.Value, publicBase.Authority, StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Scheme = publicBase.Scheme;
            }

            return next(context);
        });
    }

    /// <summary>
    /// Issues the readable XSRF-TOKEN cookie for the current user. Antiforgery tokens are bound to the signed-in user,
    /// so a new one is issued whenever the user changes (sign in, sign out) and on GET /api/me.
    /// </summary>
    public static void IssueXsrfToken(HttpContext context)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(XsrfCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // Angular must read it to copy it into the header
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    /// <summary>Requires a valid CSRF token (cookie + X-XSRF-TOKEN header) on the endpoint.</summary>
    public static RouteHandlerBuilder RequireCsrfToken(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Json(
                    new { error = new { code = "CSRF_TOKEN_INVALID", message = "The request could not be verified. Reload the page and try again." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });

    private static Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { error = new { code, message } });
    }
}
