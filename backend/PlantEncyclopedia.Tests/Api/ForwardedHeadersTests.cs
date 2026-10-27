using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Forwarded headers (design 2026-10-27): behind the website's nginx the API sees plain HTTP from the proxy. It
/// trusts X-Forwarded-Proto and X-Forwarded-For only from the configured proxy networks, and only when turned on.
/// Each request here is plain HTTP from a chosen address; the Secure XSRF-TOKEN cookie on GET /api/me is issued only
/// when the request counts as HTTPS.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ForwardedHeadersTests(PostgresDatabaseFixture fixture)
{
    private static readonly Dictionary<string, string?> Trusting = new()
    {
        ["ForwardedHeaders:Enabled"] = "true",
        ["ForwardedHeaders:KnownNetworks:0"] = "10.9.0.0/16"
    };

    // Protects against: secure cookies and CSRF failing behind the website's proxy (the Docker stack).
    [Fact]
    public async Task HttpsFromATrustedProxy_IsHonoured()
    {
        await using var factory = Factory(Trusting, "10.9.0.5");

        var response = await GetMeAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(SetsXsrfCookie(response));
    }

    // Protects against: any client claiming HTTPS (or another address, dodging rate limits) with a forged header.
    [Fact]
    public async Task HeadersFromAnUntrustedAddress_AreIgnored()
    {
        await using var factory = Factory(Trusting, "203.0.113.7");

        var response = await GetMeAsync(factory);

        Assert.False(SetsXsrfCookie(response));
    }

    // Protects against: forwarded headers being trusted when nothing was configured (the normal case today).
    [Fact]
    public async Task ForwardedHeaders_AreOffByDefault()
    {
        await using var factory = Factory(new Dictionary<string, string?>(), "10.9.0.5");

        var response = await GetMeAsync(factory);

        Assert.False(SetsXsrfCookie(response));
    }

    // Protects against: a typo in the proxy network silently trusting nobody (or everybody).
    [Fact]
    public async Task AnInvalidNetwork_StopsStartup()
    {
        await using var factory = Factory(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownNetworks:0"] = "not-a-network"
        }, "10.9.0.5");

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(Chain(error), e => e is InvalidOperationException && e.Message.Contains("not-a-network"));
    }

    private WebApplicationFactory<Program> Factory(Dictionary<string, string?> settings, string remoteAddress) =>
        new TestApiFactory(fixture.ConnectionString).WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(IPAddress.Parse(remoteAddress))));
        });

    private static async Task<HttpResponseMessage> GetMeAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "198.51.100.20");
        return await client.SendAsync(request);
    }

    private static bool SetsXsrfCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException) yield return current;
    }

    /// <summary>Makes every request arrive from the given address, before the app's own middleware runs.</summary>
    private sealed class RemoteAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
