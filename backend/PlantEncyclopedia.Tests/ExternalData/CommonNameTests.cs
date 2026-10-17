using System.Net;
using System.Text;
using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.ChecklistBank;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>
/// The ChecklistBank client against recorded responses (Fixtures/ChecklistBank) and simulated failures.
/// Offline: every request is answered by <see cref="StubHttpMessageHandler"/>.
/// </summary>
public class ChecklistBankCommonNameClientTests
{
    // Protects against: asking another dataset or host, plain HTTP, or an anonymous User-Agent.
    [Fact]
    public async Task Get_SendsOneGetToChecklistBank_ForTheColXrTaxon()
    {
        var handler = StubHttpMessageHandler.Returning(() => ChecklistBankFixtures.Response(ChecklistBankFixtures.RosaCanina));

        await ChecklistBankFixtures.Client(handler).GetAsync("4TCZM", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.checklistbank.org/dataset/3LXR/taxon/4TCZM/vernacular", request.RequestUri!.AbsoluteUri);
        var userAgent = request.Headers.UserAgent.ToString();
        Assert.StartsWith("PlantEncyclopedia/", userAgent);
        Assert.EndsWith($"(+{GbifFixtures.Contact})", userAgent);
    }

    // Protects against: losing names or references while reading the recorded response.
    [Fact]
    public async Task Get_ReadsRecordedNames()
    {
        var names = await GetFixtureAsync(ChecklistBankFixtures.RosaCanina);

        Assert.Equal(119, names.Count);
        Assert.Contains(new ExternalCommonName("dog rose", "eng", HasReference: true), names);
        Assert.Contains(new ExternalCommonName("Dog-rose", "eng", HasReference: false), names);
        Assert.Contains(new ExternalCommonName("Hundsrose", "deu", HasReference: false), names);
    }

    public static TheoryData<string, Func<HttpResponseMessage>> FailedResponses => new()
    {
        { "unknown taxon", () => new HttpResponseMessage(HttpStatusCode.NotFound) },
        { "server error", () => new HttpResponseMessage(HttpStatusCode.InternalServerError) },
        { "redirect", () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.org/") } } },
        { "html", () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html></html>", Encoding.UTF8, "text/html") } },
        { "invalid json", () => GbifFixtures.Json("[{\"name\":") },
        { "object instead of array", () => GbifFixtures.Json("{\"name\":\"dog rose\"}") },
        { "over 1 MB", () => GbifFixtures.Json("[{\"name\":\"" + new string('a', ChecklistBankCommonNameClient.MaxResponseBytes) + "\"}]") }
    };

    // Protects against: a problem with optional data failing the import or escaping as an exception.
    [Theory]
    [MemberData(nameof(FailedResponses))]
    public async Task Get_ReturnsNoNames_WhenResponseIsUnusable(string scenario, Func<HttpResponseMessage> response)
    {
        var handler = StubHttpMessageHandler.Returning(response);

        var names = await ChecklistBankFixtures.Client(handler).GetAsync("4TCZM", CancellationToken.None);

        Assert.True(names.Count == 0, scenario);
        Assert.Single(handler.Requests); // no retries
    }

    [Fact]
    public async Task Get_ReturnsNoNames_WhenNetworkFails()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException(HttpRequestError.NameResolutionError, "no such host"));

        Assert.Empty(await ChecklistBankFixtures.Client(handler).GetAsync("4TCZM", CancellationToken.None));
    }

    // Protects against: a slow ChecklistBank pushing the lookup past the endpoint's 5-second timeout.
    [Fact]
    public async Task Get_ReturnsNoNames_WhenSlow()
    {
        Assert.Equal(TimeSpan.FromSeconds(1.5), ChecklistBankCommonNameClient.Timeout);

        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        var started = DateTime.UtcNow;
        var names = await ChecklistBankFixtures.Client(handler).GetAsync("4TCZM", CancellationToken.None);

        Assert.Empty(names);
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Get_Throws_WhenCallerCancels()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ChecklistBankFixtures.Client(handler).GetAsync("4TCZM", cancellation.Token));
    }

    // Protects against: an ID of dots being read as a path segment ("taxon/../vernacular").
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task Get_SendsNothing_ForDotIds(string id)
    {
        var handler = StubHttpMessageHandler.Returning(() => ChecklistBankFixtures.Response(ChecklistBankFixtures.RosaCanina));

        Assert.Empty(await ChecklistBankFixtures.Client(handler).GetAsync(id, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    internal static Task<IReadOnlyList<ExternalCommonName>> GetFixtureAsync(string fixture) =>
        ChecklistBankFixtures.Client(StubHttpMessageHandler.Returning(() => ChecklistBankFixtures.Response(fixture)))
            .GetAsync("recorded", CancellationToken.None);
}

public class CommonNamePolicyTests
{
    // Protects against: importing names in languages the app does not support, losing names it does,
    // or showing no English name for a plant whose sources agree on one ("Dog-rose" and "dog rose").
    [Fact]
    public async Task RecordedRosaCanina_KeepsSupportedLanguages_AndShowsDogRose()
    {
        var selected = CommonNamePolicy.Select(await ChecklistBankCommonNameClientTests.GetFixtureAsync(ChecklistBankFixtures.RosaCanina));

        Assert.Equal("Dog rose", selected.EnglishDisplayName);
        Assert.Equal(["de", "en", "fr"], selected.Names.Select(n => n.LanguageCode).Distinct().Order());
        Assert.Contains(new SelectedCommonName("dog rose", "en"), selected.Names);
        Assert.Contains(new SelectedCommonName("Dog-rose", "en"), selected.Names);
        Assert.Contains(new SelectedCommonName("Hunds-Rose", "de"), selected.Names);
        Assert.Contains(new SelectedCommonName("Églantier", "fr"), selected.Names);
        Assert.Contains(new SelectedCommonName("Eglantier", "fr"), selected.Names);
        Assert.Equal(6 + 4 + 7, selected.Names.Count);
    }

    // Protects against: picking an arbitrary English name when each is listed once; the only one with a
    // cited reference wins.
    [Fact]
    public async Task RecordedMentha_ShowsPeppermint()
    {
        var selected = CommonNamePolicy.Select(await ChecklistBankCommonNameClientTests.GetFixtureAsync(ChecklistBankFixtures.MenthaHybrid));

        Assert.Equal("Peppermint", selected.EnglishDisplayName);
        Assert.Contains(new SelectedCommonName("Pfefferminze", "de"), selected.Names);
        Assert.Contains(new SelectedCommonName("menthe poivrée", "fr"), selected.Names);
    }

    // Protects against: guessing when the sources do not single out one English name.
    [Fact]
    public void NoEnglishName_WhenTiedWithoutOneReference()
    {
        Assert.Null(CommonNamePolicy.Select([Name("hedge rose"), Name("brier")]).EnglishDisplayName);
        Assert.Null(CommonNamePolicy.Select([Name("hedge rose", reference: true), Name("brier", reference: true)]).EnglishDisplayName);
        Assert.Null(CommonNamePolicy.Select([Name("Hundsrose", "deu")]).EnglishDisplayName);
        Assert.Null(CommonNamePolicy.Select([]).EnglishDisplayName);
    }

    [Fact]
    public void SingleEnglishName_IsShown_Capitalized()
    {
        var selected = CommonNamePolicy.Select([Name("  sweet   basil "), Name("바질", "kor")]);

        Assert.Equal("Sweet basil", selected.EnglishDisplayName);
        Assert.Equal([new SelectedCommonName("sweet basil", "en"), new SelectedCommonName("바질", "ko")], selected.Names);
    }

    // Protects against: a unique-index violation on plant_names, and keeping an unreferenced spelling
    // over a referenced one.
    [Fact]
    public void CaseDuplicates_AreStoredOnce_PreferringTheReferencedSpelling()
    {
        var selected = CommonNamePolicy.Select([Name("Dog Rose"), Name("dog rose", reference: true), Name("dog rose")]);

        Assert.Equal([new SelectedCommonName("dog rose", "en")], selected.Names);
        Assert.Equal("Dog rose", selected.EnglishDisplayName);
    }

    // Protects against: storing malformed names, names without a language, or unbounded numbers of rows.
    [Fact]
    public void InvalidNames_AreDropped_AndTheCountIsCapped()
    {
        var invalid = new[]
        {
            Name("bad\u0007name"), Name(new string('a', 201)), Name("1234"), Name("   "),
            Name("dog rose", language: null), Name("dog rose", "xx"), Name("rosa canina", "lat")
        };
        Assert.Empty(CommonNamePolicy.Select(invalid).Names);

        var many = Enumerable.Range(0, 100).Select(i => Name($"name {(char)('a' + i % 26)}{i}"));
        Assert.Equal(CommonNamePolicy.MaxNames, CommonNamePolicy.Select(many).Names.Count);
    }

    private static ExternalCommonName Name(string name, string? language = "eng", bool reference = false) =>
        new(name, language, reference);
}
