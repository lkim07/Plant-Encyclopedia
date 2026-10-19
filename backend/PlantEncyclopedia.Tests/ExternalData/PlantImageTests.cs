using System.Net;
using System.Text;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>
/// Automatic image choice on recorded iNaturalist and Wikimedia responses. Offline: every request is answered
/// by <see cref="StubHttpMessageHandler"/>, and nothing is downloaded.
/// </summary>
public class PlantImageSelectorTests
{
    // Protects against: choosing a photo that forbids commercial use (the first two Rosa canina photos are
    // CC BY-NC-ND and CC BY-NC), or asking Wikipedia when iNaturalist already has a usable photo.
    [Fact]
    public async Task RosaCanina_GetsTheFirstCommerciallyUsableINaturalistPhoto_AndAPublicDomainDrawing()
    {
        var handler = ImageFixtures.RecordedHandler();

        var selected = await ImageFixtures.Selector(handler).SelectAsync("Rosa canina", CancellationToken.None);

        var photo = selected.Photo!;
        Assert.Equal(("inaturalist", "230621206", "cc-by-sa", "CC BY-SA", "Douglas Goldman"),
            (photo.Image.Provider, photo.Image.SourceId, photo.LicenseCode, photo.LicenseName, photo.Creator));
        Assert.Equal("https://inaturalist-open-data.s3.amazonaws.com/photos/230621206/original.jpg", photo.Image.DownloadUrl.AbsoluteUri);
        Assert.Null(photo.LicenseUrl); // iNaturalist gives no licence version

        var drawing = selected.Illustration!;
        Assert.Equal(("wikimedia-commons", "File:Bley, Botanisches Bilderbuch 23.jpg", "pd", "Franz Bley & H. Berdrow"),
            (drawing.Image.Provider, drawing.Image.SourceId, drawing.LicenseCode, drawing.Creator));
        Assert.Equal((1600, 2400), (drawing.Image.Width, drawing.Image.Height));

        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Host == "en.wikipedia.org");
    }

    // Protects against: using a photo whose required credit cannot be given (iNaturalist has only NC/ND photos
    // of peppermint; the Wikipedia photo has no author), missing a usable photo in the species' Commons
    // category, or picking a photo of another organism filed there too (a grasshopper on mint).
    [Fact]
    public async Task MenthaPiperita_GetsAPhotoFromTheCommonsSpeciesCategory()
    {
        var handler = ImageFixtures.RecordedHandler();

        var selected = await ImageFixtures.Selector(handler).SelectAsync("Mentha × piperita", [PlantImageKind.Photo], CancellationToken.None);

        var photo = Assert.Single(selected);
        Assert.Equal(("wikimedia-commons", "File:Botanischer Garten Düsseldorf-0057.jpg", "cc-by-sa-4.0", "Björn Sothmann"),
            (photo.Image.Provider, photo.Image.SourceId, photo.LicenseCode, photo.Creator));
        Assert.Equal(
            ["api.inaturalist.org", "api.inaturalist.org", "en.wikipedia.org", "commons.wikimedia.org", "commons.wikimedia.org"],
            handler.Requests.Select(r => r.RequestUri!.Host));
    }

    [Theory]
    [InlineData(new[] { "Mentha × piperita", "Taken with Canon EOS 7D", "Self-published work" }, true)]
    [InlineData(new[] { "Rosa canina", "Rosales in Hockenheim", "Pink and red" }, true)]
    [InlineData(new[] { "Anacridium aegyptium", "Mentha × piperita" }, false)]
    [InlineData(new[] { "Rosa canina", "Hippophae rhamnoides" }, false)]
    [InlineData(new[] { "Rosa canina", "Rosa canina - botanical illustrations" }, false)]
    [InlineData(new[] { "Rosa canina", "Drawings of roses" }, false)]
    public void CommonsSpeciesPhotos_SkipFilesOfOtherTaxaAndDrawings(string[] categories, bool expected) =>
        Assert.Equal(expected, WikimediaClient.IsOnlyOfThisPlant(categories, categories.Contains("Rosa canina") ? "Rosa canina" : "Mentha × piperita"));

    // Protects against: sources being asked in the wrong order, or a photo source supplying a drawing.
    [Fact]
    public async Task Sources_AreAskedInOrder_AndKindsAreNotMixed()
    {
        var asked = new List<string>();
        var drawingAsPhoto = Candidate("first", PlantImageKind.Illustration);
        var selector = new PlantImageSelector(
        [
            new FakeSource("first", PlantImageKind.Photo, asked, drawingAsPhoto),
            new FakeSource("second", PlantImageKind.Photo, asked, Candidate("second")),
            new FakeSource("third", PlantImageKind.Photo, asked, Candidate("third")),
            new FakeSource("drawings", PlantImageKind.Illustration, asked)
        ]);

        var selected = await selector.SelectAsync("Rosa canina", CancellationToken.None);

        Assert.Equal("second", selected.Photo!.Image.SourceId);
        Assert.Null(selected.Illustration);
        Assert.Equal(["first", "second", "drawings"], asked);
    }

    internal static ExternalImageCandidate Candidate(
        string sourceId = "photo-1",
        PlantImageKind kind = PlantImageKind.Photo,
        string? license = "cc-by",
        string? creator = "A. Photographer",
        int width = 2048,
        int height = 1536,
        string? mime = "image/jpeg",
        bool attributionRequired = false,
        string url = "https://images.example.org/photo.jpg") =>
        new("test", sourceId, kind, new Uri(url), width, height, mime, license, attributionRequired, creator, null);

    private sealed class FakeSource(string name, PlantImageKind kind, List<string> asked, params ExternalImageCandidate[] candidates)
        : IPlantImageSource
    {
        public PlantImageKind Kind => kind;

        public Task<IReadOnlyList<ExternalImageCandidate>> GetCandidatesAsync(string scientificName, CancellationToken cancellationToken)
        {
            asked.Add(name);
            return Task.FromResult<IReadOnlyList<ExternalImageCandidate>>(candidates);
        }
    }
}

public class ExternalImagePolicyTests
{
    // Protects against: licences that forbid commercial use or changes, unknown licences, or none at all.
    [Theory]
    [InlineData("pd", "pd", "Public domain")]
    [InlineData("cc0", "cc0", "CC0")]
    [InlineData("CC0-1.0", "cc0", "CC0")]
    [InlineData("cc-by", "cc-by", "CC BY")]
    [InlineData("cc-by-4.0", "cc-by-4.0", "CC BY 4.0")]
    [InlineData("cc-by-sa", "cc-by-sa", "CC BY-SA")]
    [InlineData("cc-by-sa-3.0", "cc-by-sa-3.0", "CC BY-SA 3.0")]
    [InlineData("cc-by-2.5", "cc-by-2.5", "CC BY 2.5")]
    [InlineData("cc-by-nc", null, null)]
    [InlineData("cc-by-nc-sa", null, null)]
    [InlineData("cc-by-nd", null, null)]
    [InlineData("cc-by-nc-nd-4.0", null, null)]
    [InlineData("cc-by-sa-2.5-nl", null, null)]
    [InlineData("gfdl", null, null)]
    [InlineData("", null, null)]
    [InlineData(null, null, null)]
    public void Licenses(string? code, string? expectedCode, string? expectedName)
    {
        var rejection = ExternalImagePolicy.Evaluate(PlantImageSelectorTests.Candidate(license: code), out var accepted);

        if (expectedCode is null)
        {
            Assert.Equal(ImageRejection.LicenseNotAllowed, rejection);
        }
        else
        {
            Assert.Null(rejection);
            Assert.Equal((expectedCode, expectedName), (accepted!.LicenseCode, accepted.LicenseName));
        }
    }

    [Fact]
    public void LicenseUrl_OnlyWhenTheVersionIsKnown()
    {
        ExternalImagePolicy.Evaluate(PlantImageSelectorTests.Candidate(license: "cc-by-sa-3.0"), out var versioned);
        ExternalImagePolicy.Evaluate(PlantImageSelectorTests.Candidate(license: "cc-by-sa"), out var unversioned);

        Assert.Equal("https://creativecommons.org/licenses/by-sa/3.0/", versioned!.LicenseUrl!.AbsoluteUri);
        Assert.Null(unversioned!.LicenseUrl);
    }

    // Protects against: showing an image whose licence requires credit without being able to give it.
    [Fact]
    public void Credit_IsRequiredByCcByLicencesAndByTheProvider()
    {
        Assert.Equal(ImageRejection.MissingCreator, Evaluate(PlantImageSelectorTests.Candidate(license: "cc-by", creator: " ")));
        Assert.Equal(ImageRejection.MissingCreator, Evaluate(PlantImageSelectorTests.Candidate(license: "pd", creator: null, attributionRequired: true)));
        Assert.Null(Evaluate(PlantImageSelectorTests.Candidate(license: "pd", creator: null)));
        Assert.Null(Evaluate(PlantImageSelectorTests.Candidate(license: "cc0", creator: null)));
    }

    public static TheoryData<string, ExternalImageCandidate, ImageRejection> Rejected => new()
    {
        { "too small", PlantImageSelectorTests.Candidate(width: 999, height: 700), ImageRejection.TooSmall },
        { "tiff", PlantImageSelectorTests.Candidate(mime: "image/tiff"), ImageRejection.UnsupportedType },
        { "svg", PlantImageSelectorTests.Candidate(mime: "image/svg+xml"), ImageRejection.UnsupportedType },
        { "no type", PlantImageSelectorTests.Candidate(mime: null), ImageRejection.UnsupportedType },
        { "plain http", PlantImageSelectorTests.Candidate(url: "http://images.example.org/photo.jpg"), ImageRejection.InvalidData },
        { "no size", PlantImageSelectorTests.Candidate(width: 0), ImageRejection.InvalidData },
        { "absurd size", PlantImageSelectorTests.Candidate(width: 30_000), ImageRejection.InvalidData },
        { "control character in creator", PlantImageSelectorTests.Candidate(creator: "A\u0007B"), ImageRejection.InvalidData }
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Rejects(string scenario, ExternalImageCandidate candidate, ImageRejection expected) =>
        Assert.True(Evaluate(candidate) == expected, scenario);

    private static ImageRejection? Evaluate(ExternalImageCandidate candidate) => ExternalImagePolicy.Evaluate(candidate, out _);
}

public class ImageSourceTests
{
    // Protects against: another host, plain HTTP, an anonymous User-Agent, or a badly escaped name.
    [Fact]
    public async Task INaturalist_SearchesThenReadsTheExactTaxon()
    {
        var handler = ImageFixtures.RecordedHandler();

        await ImageFixtures.INaturalist(handler).GetCandidatesAsync("Mentha × piperita", CancellationToken.None);

        Assert.Equal(
            ["https://api.inaturalist.org/v1/taxa?q=Mentha%20%C3%97%20piperita&is_active=true&per_page=10",
             "https://api.inaturalist.org/v1/taxa/181525"],
            handler.Requests.Select(r => r.RequestUri!.AbsoluteUri));
        var userAgent = handler.Requests[0].Headers.UserAgent.ToString();
        Assert.StartsWith("PlantEncyclopedia/", userAgent);
        Assert.EndsWith($"(+{GbifFixtures.Contact})", userAgent);
    }

    // Protects against: offering "all rights reserved" photos (served from static.inaturalist.org), or photos
    // of other taxa. The order is iNaturalist's curator order.
    [Fact]
    public async Task INaturalist_OffersOpenDataPhotosOfTheTaxon_InOrder()
    {
        var candidates = await ImageFixtures.INaturalist(ImageFixtures.RecordedHandler()).GetCandidatesAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(9, candidates.Count);
        Assert.Equal(("43901474", "cc-by-nc-nd", "image/jpeg", 2048, 1365),
            (candidates[0].SourceId, candidates[0].LicenseCode, candidates[0].MimeType, candidates[0].Width, candidates[0].Height));
        Assert.All(candidates, c => Assert.Equal("inaturalist-open-data.s3.amazonaws.com", c.DownloadUrl.Host));
    }

    // Protects against: taking photos of a section, a hybrid or a mollusc that only contain the name.
    [Fact]
    public async Task INaturalist_OffersNothing_WithoutAnExactActivePlantTaxon()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(GbifFixtures.Json(ImageFixtures.Read("INaturalist/taxa-search-rosa-canina.json"))));

        var candidates = await ImageFixtures.INaturalist(handler).GetCandidatesAsync("Rosa caninae", CancellationToken.None);

        Assert.Empty(candidates);
        Assert.Single(handler.Requests);
    }

    public static TheoryData<string, Func<HttpResponseMessage>> FailedResponses => new()
    {
        { "rate limited", () => new HttpResponseMessage(HttpStatusCode.TooManyRequests) },
        { "server error", () => new HttpResponseMessage(HttpStatusCode.InternalServerError) },
        { "redirect", () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.org/") } } },
        { "html", () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html></html>", Encoding.UTF8, "text/html") } },
        { "invalid json", () => GbifFixtures.Json("{\"results\": [") },
        { "over 1 MB", () => GbifFixtures.Json("{\"results\":[],\"padding\":\"" + new string('a', 1024 * 1024) + "\"}") }
    };

    // Protects against: a failing image source breaking the import; it simply offers nothing.
    [Theory]
    [MemberData(nameof(FailedResponses))]
    public async Task Sources_OfferNothing_WhenResponseIsUnusable(string scenario, Func<HttpResponseMessage> response)
    {
        var handler = StubHttpMessageHandler.Returning(response);
        var wikimedia = ImageFixtures.Wikimedia(handler);

        Assert.True((await ImageFixtures.INaturalist(handler).GetCandidatesAsync("Rosa canina", CancellationToken.None)).Count == 0, scenario);
        Assert.True((await new WikipediaPhotoSource(wikimedia).GetCandidatesAsync("Rosa canina", CancellationToken.None)).Count == 0, scenario);
        Assert.True((await new CommonsIllustrationSource(wikimedia).GetCandidatesAsync("Rosa canina", CancellationToken.None)).Count == 0, scenario);
        Assert.Equal(3, handler.Requests.Count); // one each, no retries
    }

    [Fact]
    public async Task Sources_Throw_WhenCallerCancels()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ImageFixtures.INaturalist(handler).GetCandidatesAsync("Rosa canina", cancellation.Token));
    }

    // Protects against: losing the licence, author or file page of the Wikipedia photo.
    [Fact]
    public async Task Wikipedia_OffersTheArticlesMainImage_WithItsCommonsLicence()
    {
        var handler = ImageFixtures.RecordedHandler();
        var source = new WikipediaPhotoSource(ImageFixtures.Wikimedia(handler));

        var candidate = Assert.Single(await source.GetCandidatesAsync("Rosa canina", CancellationToken.None));

        Assert.Equal(
            ("File:Divlja ruza cvijet 270508.jpg", "cc-by-sa-3.0", true, "Roberta F.", 1024, 1365, "image/jpeg"),
            (candidate.SourceId, candidate.LicenseCode, candidate.ProviderRequiresAttribution, candidate.Creator,
             candidate.Width, candidate.Height, candidate.MimeType));
        Assert.Equal("https://commons.wikimedia.org/wiki/File:Divlja_ruza_cvijet_270508.jpg", candidate.SourcePageUrl!.AbsoluteUri);
        Assert.StartsWith("https://upload.wikimedia.org/", candidate.DownloadUrl.AbsoluteUri);
        Assert.Null(ExternalImagePolicy.Evaluate(candidate, out _));
        Assert.All(handler.Requests, r => Assert.Equal("https", r.RequestUri!.Scheme));
    }

    // Protects against: crediting nobody for a CC BY photo whose Commons page has no author.
    [Fact]
    public async Task Wikipedia_PeppermintPhoto_IsRejectedForMissingAuthor()
    {
        var source = new WikipediaPhotoSource(ImageFixtures.Wikimedia(ImageFixtures.RecordedHandler()));

        var candidate = Assert.Single(await source.GetCandidatesAsync("Mentha × piperita", CancellationToken.None));

        Assert.Equal(("File:Pfefferminze natur peppermint.jpg", "cc-by-3.0", null), (candidate.SourceId, candidate.LicenseCode, candidate.Creator));
        Assert.Equal(ImageRejection.MissingCreator, ExternalImagePolicy.Evaluate(candidate, out _));
    }

    // Protects against: showing the genus article's image (another species) when a species redirects to it.
    [Fact]
    public async Task Wikipedia_IgnoresRedirectsToTheGenus()
    {
        const string redirectedToGenus =
            """{"batchcomplete":true,"query":{"redirects":[{"from":"Rosa obscura","to":"Rosa"}],"pages":[{"pageid":1,"ns":0,"title":"Rosa","pageimage":"Rose.jpg"}]}}""";
        var handler = StubHttpMessageHandler.Returning(() => GbifFixtures.Json(redirectedToGenus));

        Assert.Empty(await new WikipediaPhotoSource(ImageFixtures.Wikimedia(handler)).GetCandidatesAsync("Rosa obscura", CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    // Protects against: offering restricted or non-free files, or files hosted elsewhere.
    [Fact]
    public async Task Commons_DropsRestrictedNonFreeAndForeignFiles()
    {
        static string File(string title, string extra = "", string url = "https://upload.wikimedia.org/a.jpg") =>
            "{\"title\":\"" + title + "\",\"imageinfo\":[{\"url\":\"" + url + "\",\"width\":2000,\"height\":2000," +
            "\"descriptionurl\":\"https://commons.wikimedia.org/wiki/" + title + "\",\"mime\":\"image/jpeg\",\"extmetadata\":{" +
            "\"License\":{\"value\":\"pd\"}" + extra + "}}]}";
        var json = "{\"query\":{\"pages\":[" + string.Join(",",
            File("File:Ok.jpg"),
            File("File:Trademark.jpg", ",\"Restrictions\":{\"value\":\"trademarked\"}"),
            File("File:NonFree.jpg", ",\"NonFree\":{\"value\":\"true\"}"),
            File("File:Elsewhere.jpg", url: "https://example.org/a.jpg")) + "]}}";
        var handler = StubHttpMessageHandler.Returning(() => GbifFixtures.Json(json));

        var candidates = await new CommonsIllustrationSource(ImageFixtures.Wikimedia(handler)).GetCandidatesAsync("Rosa canina", CancellationToken.None);

        Assert.Equal(["File:Ok.jpg"], candidates.Select(c => c.SourceId));
    }

    // Protects against: small scans, TIFFs, "No restrictions" files without a licence, and CC BY files without an author.
    [Fact]
    public async Task Commons_RecordedRosaIllustrations_AreFilteredByThePolicy()
    {
        var source = new CommonsIllustrationSource(ImageFixtures.Wikimedia(ImageFixtures.RecordedHandler()));

        var results = (await source.GetCandidatesAsync("Rosa canina", CancellationToken.None))
            .ToDictionary(c => c.SourceId, c => ExternalImagePolicy.Evaluate(c, out _));

        Assert.Equal(10, results.Count);
        Assert.Equal(ImageRejection.TooSmall, results["File:AnnePrattDogRose.JPG"]);
        Assert.Equal(ImageRejection.MissingCreator, results["File:Dog rose (Rosa canina); flowering stem. Wat Wellcome V0043243.jpg"]);
        Assert.Null(results["File:Bley, Botanisches Bilderbuch 23.jpg"]);
        Assert.Contains(ImageRejection.UnsupportedType, results.Values);
        Assert.Contains(ImageRejection.LicenseNotAllowed, results.Values);
    }

    [Theory]
    [InlineData("<a href=\"//commons.wikimedia.org/wiki/User:Roberta_F.\" title=\"User:Roberta F.\">Roberta F.</a>", "Roberta F.")]
    [InlineData("Franz Bley &amp; H. Berdrow", "Franz Bley & H. Berdrow")]
    [InlineData("<span>Unknown author</span><span>Unknown author</span>", "Unknown author Unknown author")]
    [InlineData("<br/>", null)]
    [InlineData(null, null)]
    public void Commons_AuthorHtml_BecomesPlainText(string? html, string? expected) =>
        Assert.Equal(expected, WikimediaClient.PlainText(html));
}
