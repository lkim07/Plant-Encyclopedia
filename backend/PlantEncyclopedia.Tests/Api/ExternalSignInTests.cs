using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Api.Accounts;
using PlantEncyclopedia.Infrastructure.Identity;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Google and GitHub sign-in (D-070, Phase 6a-2). The account-matching rules run against the test database;
/// the endpoints are checked up to the redirect to the provider. No provider is ever contacted.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ExternalSignInTests(PostgresDatabaseFixture fixture)
{
    // Protects against: a returning provider user getting a second account, or a new one not being created.
    [Fact]
    public async Task NewProviderUser_GetsAnAccount_AndSignsIntoItNextTime()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var email = NewEmail();
        var identity = new ExternalIdentity(ExternalSignIn.Google, Guid.NewGuid().ToString("N"), email, EmailVerified: true, "Rose Lover");

        var (first, user) = await SignInAsync(factory, identity);
        var (second, again) = await SignInAsync(factory, identity);

        Assert.Equal((ExternalSignInStatus.Created, ExternalSignInStatus.SignedIn), (first, second));
        Assert.Equal(user!.Id, again!.Id);
        Assert.Equal((email, "Rose Lover", true), (user.Email, user.DisplayName, user.EmailConfirmed));
    }

    // Protects against: a provider that proves the address not being linked to the existing account (a duplicate),
    // and the account staying unconfirmed although the provider proved ownership.
    [Fact]
    public async Task VerifiedProviderEmail_LinksToTheExistingAccount_AndConfirmsIt()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var existing = await CreatePasswordAccountAsync(factory);

        var (status, user) = await SignInAsync(factory,
            new ExternalIdentity(ExternalSignIn.Google, Guid.NewGuid().ToString("N"), existing.Email!.ToUpperInvariant(), EmailVerified: true, "Someone"));

        Assert.Equal(ExternalSignInStatus.Linked, status);
        Assert.Equal(existing.Id, user!.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await users.FindByIdAsync(existing.Id.ToString()))!;
        Assert.True(stored.EmailConfirmed);
        Assert.Equal(["Google"], (await users.GetLoginsAsync(stored)).Select(l => l.LoginProvider));
    }

    // Protects against: account takeover by registering someone else's address at a provider that does not verify it.
    [Fact]
    public async Task UnverifiedProviderEmail_NeverTakesOverAnExistingAccount()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var existing = await CreatePasswordAccountAsync(factory);

        var (status, user) = await SignInAsync(factory,
            new ExternalIdentity(ExternalSignIn.GitHub, Guid.NewGuid().ToString("N"), existing.Email, EmailVerified: false, "Mallory"));

        Assert.Equal((ExternalSignInStatus.EmailInUse, null), (status, user));
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Empty(await users.GetLoginsAsync((await users.FindByIdAsync(existing.Id.ToString()))!));
    }

    // Protects against: an unverified address counting as confirmed (it would allow saving Favorites unconfirmed),
    // or an account without an email address (every account needs one).
    [Fact]
    public async Task NewAccounts_AreConfirmedOnlyWhenTheProviderVerifiedTheEmail()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);

        var (created, unconfirmed) = await SignInAsync(factory, new ExternalIdentity(ExternalSignIn.GitHub, Guid.NewGuid().ToString("N"), NewEmail(), false, "Mia"));
        var (status, noEmail) = await SignInAsync(factory, new ExternalIdentity(ExternalSignIn.GitHub, Guid.NewGuid().ToString("N"), null, false, null));

        Assert.Equal((ExternalSignInStatus.Created, false), (created, unconfirmed!.EmailConfirmed));
        Assert.Equal((ExternalSignInStatus.EmailRequired, null), (status, noEmail));
    }

    [Fact]
    public async Task DisplayNames_FallBackToTheEmail_AndFitTheColumn()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var email = NewEmail();

        var (_, unnamed) = await SignInAsync(factory, new ExternalIdentity(ExternalSignIn.Google, Guid.NewGuid().ToString("N"), email, true, null));
        var (_, longName) = await SignInAsync(factory, new ExternalIdentity(ExternalSignIn.Google, Guid.NewGuid().ToString("N"), NewEmail(), true, new string('n', 80)));

        Assert.Equal(email.Split('@')[0], unnamed!.DisplayName);
        Assert.Equal(ApplicationUser.DisplayNameMaxLength, longName!.DisplayName.Length);
    }

    // Protects against: trusting a provider's "verified" claim from a provider other than Google.
    [Theory]
    [InlineData("Google", "true", true)]
    [InlineData("Google", "True", true)]
    [InlineData("Google", "false", false)]
    [InlineData("Google", null, false)]
    [InlineData("GitHub", "true", false)]
    public void ProviderIdentity_TrustsOnlyGooglesVerifiedFlag(string provider, string? verified, bool expected)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, " a@example.org "), new(ClaimTypes.Name, "A") };
        if (verified is not null) claims.Add(new Claim(ExternalIdentity.EmailVerifiedClaim, verified));
        var info = new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), provider, "key", provider);

        var identity = ExternalIdentity.From(info);

        Assert.Equal((expected, "a@example.org"), (identity.EmailVerified, identity.Email));
    }

    // Protects against: offering buttons for providers that are not set up.
    [Fact]
    public async Task Providers_ListsOnlyConfiguredOnes()
    {
        await using var none = new TestApiFactory(fixture.ConnectionString);
        Assert.Equal("[]", await DataAsync(none.CreateClient(), "/api/auth/providers"));

        await using var google = WithGoogle();
        Assert.Equal("""[{"id":"google","name":"Google"}]""", await DataAsync(google.CreateClient(), "/api/auth/providers"));
    }

    // Protects against: sending the user to the provider with the wrong callback, or starting an unknown provider.
    [Fact]
    public async Task Start_RedirectsToTheProvider_WithTheSiteCallback()
    {
        await using var factory = WithGoogle();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var start = await client.GetAsync("/api/auth/external/google?returnUrl=%2Fplants%2Fabc");
        var unknown = await client.GetAsync("/api/auth/external/facebook");

        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var location = start.Headers.Location!;
        Assert.Equal("accounts.google.com", location.Host);
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("https://localhost/signin-google", query["redirect_uri"]);
        Assert.Equal("test-google-client", query["client_id"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // Protects against: a callback without a provider answer signing anyone in, or returning to another site.
    [Fact]
    public async Task Callback_WithoutAProviderAnswer_GoesBackToSignIn()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/external/callback?returnUrl=%2F%2Fevil.example");

        Assert.Equal("https://localhost/login?error=external-failed&returnUrl=%2Fexplore", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/plants/abc", "/plants/abc")]
    [InlineData("//evil.example", "/explore")]
    [InlineData("/\\evil.example", "/explore")]
    [InlineData("https://evil.example", "/explore")]
    [InlineData(null, "/explore")]
    public void ReturnUrl_StaysInsideTheApp(string? value, string expected) =>
        Assert.Equal(expected, AccountEndpoints.SafeReturnUrl(value));

    private WebApplicationFactory<Program> WithGoogle() =>
        new TestApiFactory(fixture.ConnectionString).WithWebHostBuilder(b =>
        {
            b.UseSetting("Accounts:Providers:Google:ClientId", "test-google-client");
            b.UseSetting("Accounts:Providers:Google:ClientSecret", "test-google-secret");
        });

    private static async Task<string> DataAsync(HttpClient client, string path)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync(path));
        return json.RootElement.GetProperty("data").GetRawText();
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.org";

    private static async Task<(ExternalSignInStatus, ApplicationUser?)> SignInAsync(TestApiFactory factory, ExternalIdentity identity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return await scope.ServiceProvider.GetRequiredService<ExternalSignIn>().SignInAsync(identity);
    }

    private static async Task<ApplicationUser> CreatePasswordAccountAsync(TestApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Existing" };
        Assert.True((await users.CreateAsync(user, "plant lover")).Succeeded);
        return user;
    }
}
