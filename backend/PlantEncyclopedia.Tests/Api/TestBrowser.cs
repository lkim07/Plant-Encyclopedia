using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// A browser for API tests: HTTPS (the session cookie is Secure), cookies kept, redirects not followed, and the
/// XSRF-TOKEN cookie copied into the X-XSRF-TOKEN header on requests that change data, like Angular does.
/// </summary>
internal sealed class TestBrowser
{
    private string? _xsrf;

    private TestBrowser(HttpClient client) => Client = client;

    public HttpClient Client { get; }

    public static async Task<TestBrowser> OpenAsync(TestApiFactory factory)
    {
        var browser = new TestBrowser(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        }));
        await browser.MeAsync();
        return browser;
    }

    /// <summary>The signed-in user's data, or null when signed out. Also picks up the CSRF token.</summary>
    public async Task<JsonElement?> MeAsync()
    {
        var response = await Client.GetAsync("/api/me");
        Remember(response);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }

    public Task<HttpResponseMessage> PostAsync(string path, object body) => SendAsync(HttpMethod.Post, path, body);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (_xsrf is not null) request.Headers.Add("X-XSRF-TOKEN", _xsrf);
        var response = await Client.SendAsync(request);
        Remember(response);
        return response;
    }

    private void Remember(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return;
        var xsrf = cookies.FirstOrDefault(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        if (xsrf is not null) _xsrf = Uri.UnescapeDataString(xsrf["XSRF-TOKEN=".Length..].Split(';')[0]);
    }
}
