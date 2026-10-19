using System.Net;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData.Images;
using PlantEncyclopedia.Infrastructure.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.INaturalist;
using PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;
using PlantEncyclopedia.Tests.Api;

namespace PlantEncyclopedia.Tests.ExternalData;

/// <summary>
/// Recorded iNaturalist and Wikimedia responses (Fixtures/INaturalist, Fixtures/Wikimedia; see the READMEs there)
/// and the real image sources, configured as in production, talking to a stub handler.
/// </summary>
internal static class ImageFixtures
{
    /// <summary>Answers exactly the recorded requests from their files, and anything else with 404.</summary>
    public static StubHttpMessageHandler RecordedHandler() =>
        new((request, _) => Task.FromResult(Respond(request)));

    public static HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        var query = Uri.UnescapeDataString(uri.Query);
        var file = (uri.Host, uri.AbsolutePath) switch
        {
            ("api.inaturalist.org", "/v1/taxa") when query.Contains("q=Rosa canina") => "INaturalist/taxa-search-rosa-canina.json",
            ("api.inaturalist.org", "/v1/taxa") when query.Contains("q=Mentha × piperita") => "INaturalist/taxa-search-mentha-x-piperita.json",
            ("api.inaturalist.org", "/v1/taxa/55884") => "INaturalist/taxon-55884-rosa-canina.json",
            ("api.inaturalist.org", "/v1/taxa/181525") => "INaturalist/taxon-181525-mentha-x-piperita.json",
            ("en.wikipedia.org", "/w/api.php") when query.Contains("titles=Rosa canina") => "Wikimedia/wikipedia-pageimage-rosa-canina.json",
            ("en.wikipedia.org", "/w/api.php") when query.Contains("titles=Mentha × piperita") => "Wikimedia/wikipedia-pageimage-mentha-x-piperita.json",
            ("commons.wikimedia.org", "/w/api.php") when query.Contains("titles=File:Divlja_ruza_cvijet_270508.jpg") => "Wikimedia/commons-imageinfo-rosa-canina.json",
            ("commons.wikimedia.org", "/w/api.php") when query.Contains("titles=File:Pfefferminze_natur_peppermint.jpg") => "Wikimedia/commons-imageinfo-mentha-x-piperita.json",
            ("commons.wikimedia.org", "/w/api.php") when query.Contains("gcmtitle=Category:Rosa canina&") => "Wikimedia/commons-category-rosa-canina.json",
            ("commons.wikimedia.org", "/w/api.php") when query.Contains("gcmtitle=Category:Mentha × piperita&") => "Wikimedia/commons-category-mentha-x-piperita.json",
            ("commons.wikimedia.org", "/w/api.php") when query.Contains("gcmtitle=Category:Rosa canina - botanical illustrations") => "Wikimedia/commons-illustrations-rosa-canina.json",
            ("commons.wikimedia.org", "/w/api.php") when query.Contains("gcmtitle=Category:Mentha × piperita - botanical illustrations") => "Wikimedia/commons-illustrations-mentha-x-piperita.json",
            _ => null
        };

        return file is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : GbifFixtures.Json(Read(file));
    }

    public static string Read(string path) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", path));

    public static INaturalistPhotoSource INaturalist(HttpMessageHandler handler, CapturingLoggerProvider? logs = null)
    {
        var client = new HttpClient(handler);
        ExternalJsonClient.Configure(client, INaturalistPhotoSource.BaseAddress, INaturalistPhotoSource.Timeout, GbifFixtures.Options());
        return new INaturalistPhotoSource(client, Loggers(logs).CreateLogger<INaturalistPhotoSource>());
    }

    public static WikimediaClient Wikimedia(HttpMessageHandler handler, CapturingLoggerProvider? logs = null)
    {
        var client = new HttpClient(handler);
        ExternalJsonClient.Configure(client, null, WikimediaClient.Timeout, GbifFixtures.Options());
        return new WikimediaClient(client, Loggers(logs).CreateLogger<WikimediaClient>());
    }

    /// <summary>The production order: iNaturalist, Wikipedia, then the Commons species category for photos; Commons for illustrations.</summary>
    public static PlantImageSelector Selector(HttpMessageHandler handler)
    {
        var wikimedia = Wikimedia(handler);
        return new PlantImageSelector(
        [
            INaturalist(handler),
            new WikipediaPhotoSource(wikimedia),
            new CommonsCategoryPhotoSource(wikimedia),
            new CommonsIllustrationSource(wikimedia)
        ]);
    }

    private static ILoggerFactory Loggers(CapturingLoggerProvider? logs) =>
        LoggerFactory.Create(b => b.AddProvider(logs ?? new CapturingLoggerProvider()));
}
