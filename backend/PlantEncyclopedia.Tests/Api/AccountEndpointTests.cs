using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Email + password accounts (D-070, API_SPEC §6): sessions, CSRF protection, lockout, password reset and email
/// confirmation. Emails are read from the test API's email folder; nothing is sent.
/// </summary>
[Collection(PostgresCollection.Name)]
public partial class AccountEndpointTests(PostgresDatabaseFixture fixture)
{
    private const string Password = "plant lover";

    // Protects against: a session cookie readable by scripts, sent over plain HTTP, or sent by other sites'
    // requests; an account that is not signed in after registering; or no confirmation email.
    [Fact]
    public async Task Register_SignsIn_SetsASecureSessionCookie_AndEmailsAConfirmationLink()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);
        var email = NewEmail();

        var response = await browser.PostAsync("/api/auth/register", new { email, password = Password, displayName = "Rose Lover" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pe_session="));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", session, StringComparison.OrdinalIgnoreCase);

        var me = await browser.MeAsync();
        Assert.Equal((email, "Rose Lover", false), (me!.Value.GetProperty("email").GetString(), me.Value.GetProperty("displayName").GetString(),
            me.Value.GetProperty("emailConfirmed").GetBoolean()));

        var link = LinkIn(Assert.Single(EmailsTo(factory, email, "confirm-email")));
        Assert.StartsWith("https://localhost/api/auth/confirm-email?userId=", link);

        var confirm = await browser.Client.GetAsync(new Uri(link).PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        Assert.Equal("https://localhost/email-confirmed?status=ok", confirm.Headers.Location!.ToString());
        Assert.True((await browser.MeAsync())!.Value.GetProperty("emailConfirmed").GetBoolean());
    }

    // Protects against: another site making a signed-in (or signing-in) browser change the account (CSRF).
    [Fact]
    public async Task StateChangingRequests_NeedTheCsrfToken()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);
        var body = new { email = NewEmail(), password = Password, displayName = "Mallory" };

        var missing = await browser.Client.PostAsJsonAsync("/api/auth/register", body);
        browser.Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "forged");
        var forged = await browser.Client.PostAsJsonAsync("/api/auth/register", body);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (missing.StatusCode, forged.StatusCode));
        Assert.Contains("CSRF_TOKEN_INVALID", await missing.Content.ReadAsStringAsync());
    }

    public static TheoryData<string, object, string> InvalidRegistrations => new()
    {
        { "short password", new { email = "a@example.org", password = "1234567", displayName = "A" }, "password" },
        { "long password", new { email = "a@example.org", password = new string('a', 129), displayName = "A" }, "password" },
        { "no email", new { email = "", password = Password, displayName = "A" }, "email" },
        { "bad email", new { email = "not-an-email", password = Password, displayName = "A" }, "email" },
        { "no name", new { email = "a@example.org", password = Password, displayName = "  " }, "displayName" },
        { "long name", new { email = "a@example.org", password = Password, displayName = new string('n', 51) }, "displayName" }
    };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Register_ValidatesInput(string scenario, object body, string field)
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.PostAsync("/api/auth/register", body);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, scenario);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("error").GetProperty("details").TryGetProperty(field, out _), scenario);
    }

    // Protects against: an 8-character password being refused (the developer's chosen minimum), or one email
    // address holding two accounts (case-insensitively).
    [Fact]
    public async Task Register_AcceptsEightCharacters_AndRefusesATakenEmail()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.Created, (await browser.PostAsync("/api/auth/register", new { email, password = "12345678", displayName = "A" })).StatusCode);

        var other = await TestBrowser.OpenAsync(factory);
        var taken = await other.PostAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = Password, displayName = "B" });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Contains("EMAIL_TAKEN", await taken.Content.ReadAsStringAsync());
    }

    // Protects against: telling an attacker whether an address has an account at sign-in, or unlimited guessing.
    [Fact]
    public async Task Login_SameAnswerForUnknownEmailAndWrongPassword_AndLocksAfterFiveFailures()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var email = await RegisterAsync(factory);
        var browser = await TestBrowser.OpenAsync(factory);

        var unknown = await browser.PostAsync("/api/auth/login", new { email = NewEmail(), password = Password });
        var wrong = await browser.PostAsync("/api/auth/login", new { email, password = "wrong password" });
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (unknown.StatusCode, wrong.StatusCode));
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await wrong.Content.ReadAsStringAsync());

        for (var i = 0; i < 4; i++)
        {
            await browser.PostAsync("/api/auth/login", new { email, password = "wrong password" });
        }

        var locked = await browser.PostAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        Assert.Contains("ACCOUNT_LOCKED", await locked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_AndLogout_ChangeTheSession()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var email = await RegisterAsync(factory);
        var browser = await TestBrowser.OpenAsync(factory);
        Assert.Null(await browser.MeAsync());

        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        Assert.Equal(email, (await browser.MeAsync())!.Value.GetProperty("email").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Null(await browser.MeAsync());
    }

    // Protects against: "forgot password" revealing which addresses have accounts, a reset that does not change the
    // password, or a reset link that works with a forged token.
    [Fact]
    public async Task PasswordReset_RevealsNothing_AndReplacesThePassword()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var email = await RegisterAsync(factory);
        var browser = await TestBrowser.OpenAsync(factory);

        var unknown = await browser.PostAsync("/api/auth/forgot-password", new { email = NewEmail() });
        var known = await browser.PostAsync("/api/auth/forgot-password", new { email });
        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted), (unknown.StatusCode, known.StatusCode));
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());

        var link = new Uri(LinkIn(Assert.Single(EmailsTo(factory, email, "reset-password"))));
        Assert.Equal("/reset-password", link.AbsolutePath);
        var token = Regex.Match(link.Query, "token=([^&]+)").Groups[1].Value;

        var forged = await browser.PostAsync("/api/auth/reset-password", new { email, token = "Zm9yZ2Vk", password = "new password" });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Contains("INVALID_RESET_LINK", await forged.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent,
            (await browser.PostAsync("/api/auth/reset-password", new { email, token, password = "new password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.PostAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsync("/api/auth/login", new { email, password = "new password" })).StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithABadToken_SaysInvalid()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.Client.GetAsync($"/api/auth/confirm-email?userId={Guid.NewGuid()}&token=Zm9yZ2Vk");

        Assert.Equal("https://localhost/email-confirmed?status=invalid", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ResendConfirmation_NeedsASignedInUser()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);

        var response = await browser.PostAsync("/api/auth/resend-confirmation", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("UNAUTHENTICATED", await response.Content.ReadAsStringAsync());
    }

    // Protects against: unlimited password guessing from one client, and passwords in the logs.
    [Fact]
    public async Task AccountEndpoints_AreRateLimited_AndNeverLogPasswords()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);
        const string secret = "my-secret-password-123";

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            statuses.Add((await browser.PostAsync("/api/auth/login", new { email = NewEmail(), password = secret })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Contains(secret));
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.org";

    private static async Task<string> RegisterAsync(TestApiFactory factory)
    {
        var email = NewEmail();
        var browser = await TestBrowser.OpenAsync(factory);
        var response = await browser.PostAsync("/api/auth/register", new { email, password = Password, displayName = "Tester" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private static IEnumerable<string> EmailsTo(TestApiFactory factory, string email, string kind) =>
        Directory.Exists(factory.EmailFolder)
            ? Directory.GetFiles(factory.EmailFolder, $"*-{kind}-*.txt")
                .Select(File.ReadAllText)
                .Where(text => text.StartsWith($"To: {email}\n", StringComparison.Ordinal))
            : [];

    private static string LinkIn(string email) => LinkPattern().Match(email).Value;

    [GeneratedRegex(@"https://\S+")]
    private static partial Regex LinkPattern();
}
