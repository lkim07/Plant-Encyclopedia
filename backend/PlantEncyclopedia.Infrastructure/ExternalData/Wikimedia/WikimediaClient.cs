using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData.Images;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;

/// <summary>
/// Read-only MediaWiki Action API calls for the Wikipedia and Commons image sources (ARCHITECTURE §21).
/// Absolute HTTPS URLs to en.wikipedia.org and commons.wikimedia.org only, built from fixed text and the
/// escaped scientific name. Commons files become <see cref="ExternalImageCandidate"/>s with their licence,
/// author and file page; files with legal restrictions or marked non-free are dropped here.
/// </summary>
internal sealed partial class WikimediaClient(HttpClient http, ILogger<WikimediaClient> logger)
{
    public const string ProviderName = "wikimedia-commons";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const string WikipediaApi = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2";
    private const string CommonsApi = "https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2";

    // A rendition this wide is enough for the largest stored size; originals can be far larger.
    private const string ImageInfo = "&prop=imageinfo&iiprop=url%7Csize%7Cmime%7Cextmetadata&iiurlwidth=1600";

    private static readonly HashSet<string> FileHosts = ["upload.wikimedia.org", "thumb.wikimedia.org"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The English Wikipedia article's main image file name for this title, following redirects.</summary>
    public async Task<(string? FileName, string? ArticleTitle)> GetPageImageAsync(string title, CancellationToken cancellationToken)
    {
        var response = await ExternalJsonClient.GetAsync<MediaWikiResponse>(
            http, $"{WikipediaApi}&redirects=1&prop=pageimages&piprop=name%7Coriginal&titles={Uri.EscapeDataString(title)}",
            JsonOptions, "Wikipedia page image", logger, cancellationToken);

        var page = response?.Query?.Pages?.FirstOrDefault();
        return page is null || page.Missing == true ? (null, null) : (page.Pageimage, page.Title);
    }

    /// <summary>One Commons file, or none when it is not on Commons (e.g. a non-free file kept on Wikipedia).</summary>
    public async Task<IReadOnlyList<ExternalImageCandidate>> GetFileAsync(string fileName, ImageKind kind, CancellationToken cancellationToken)
    {
        var response = await ExternalJsonClient.GetAsync<MediaWikiResponse>(
            http, $"{CommonsApi}{ImageInfo}&titles={Uri.EscapeDataString("File:" + fileName)}",
            JsonOptions, "Commons file info", logger, cancellationToken);
        return ToCandidates(response, kind);
    }

    /// <summary>Up to 10 files of a Commons category, in the order the API lists them.</summary>
    public async Task<IReadOnlyList<ExternalImageCandidate>> GetCategoryFilesAsync(string category, ImageKind kind, CancellationToken cancellationToken)
    {
        var response = await ExternalJsonClient.GetAsync<MediaWikiResponse>(
            http, $"{CommonsApi}&generator=categorymembers&gcmtitle={Uri.EscapeDataString("Category:" + category)}" +
                  $"&gcmtype=file&gcmlimit=10{ImageInfo}",
            JsonOptions, "Commons category", logger, cancellationToken);
        return ToCandidates(response, kind);
    }

    private static List<ExternalImageCandidate> ToCandidates(MediaWikiResponse? response, ImageKind kind) =>
        (response?.Query?.Pages ?? [])
            .Select(p => ToCandidate(p, kind))
            .OfType<ExternalImageCandidate>()
            .ToList();

    private static ExternalImageCandidate? ToCandidate(MediaWikiPage? page, ImageKind kind)
    {
        if (page is null || page.Missing == true || page.Title is null) return null;

        var info = page.Imageinfo?.FirstOrDefault();
        if (info is null) return null;

        var metadata = info.Extmetadata ?? [];
        string? Field(string name) =>
            metadata.TryGetValue(name, out var v) && v?.Value.ValueKind == JsonValueKind.String ? v.Value.GetString() : null;

        // Trademark, personality-right or similar restrictions, or a file marked as non-free.
        if (!string.IsNullOrWhiteSpace(Field("Restrictions")) || metadata.ContainsKey("NonFree")) return null;

        // thumbwidth/thumbheight are the requested size even when Commons serves the original instead
        // ("thumbnail_unscaled", same path as the original): then the original's size is the real one.
        var (url, width, height) = IsRealThumbnail(info) && info.Thumbwidth is { } tw && info.Thumbheight is { } th
            ? (info.Thumburl, tw, th)
            : (info.Url, info.Width ?? 0, info.Height ?? 0);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var downloadUrl)
            || downloadUrl.Scheme != Uri.UriSchemeHttps
            || !FileHosts.Contains(downloadUrl.Host)
            || !Uri.TryCreate(info.Descriptionurl, UriKind.Absolute, out var pageUrl)
            || pageUrl.Scheme != Uri.UriSchemeHttps
            || pageUrl.Host != "commons.wikimedia.org")
        {
            return null;
        }

        return new ExternalImageCandidate(
            Provider: ProviderName,
            SourceId: page.Title,
            Kind: kind,
            DownloadUrl: downloadUrl,
            Width: width,
            Height: height,
            MimeType: info.Mime,
            LicenseCode: Field("License"),
            ProviderRequiresAttribution: string.Equals(Field("AttributionRequired"), "true", StringComparison.OrdinalIgnoreCase),
            Creator: PlainText(Field("Artist")) ?? PlainText(Field("Attribution")),
            SourcePageUrl: pageUrl);
    }

    private static bool IsRealThumbnail(MediaWikiImageInfo info) =>
        Uri.TryCreate(info.Thumburl, UriKind.Absolute, out var thumb)
        && Uri.TryCreate(info.Url, UriKind.Absolute, out var original)
        && thumb.AbsolutePath != original.AbsolutePath;

    /// <summary>"&lt;a href=…&gt;Roberta F.&lt;/a&gt;" becomes "Roberta F."; blank becomes null.</summary>
    internal static string? PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = WebUtility.HtmlDecode(Tags().Replace(html, " "));
        text = Whitespace().Replace(text, " ").Trim();
        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
