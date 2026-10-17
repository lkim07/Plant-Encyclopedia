using System.Text.Json;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Wikimedia;

// The parts of the MediaWiki Action API (formatversion=2) that the image sources read, from en.wikipedia.org
// (prop=pageimages) and commons.wikimedia.org (prop=imageinfo). Recorded examples:
// PlantEncyclopedia.Tests/Fixtures/Wikimedia. These types never leave the adapter.

internal sealed record MediaWikiResponse(MediaWikiQuery? Query);

internal sealed record MediaWikiQuery(List<MediaWikiRedirect?>? Redirects, List<MediaWikiPage?>? Pages);

internal sealed record MediaWikiRedirect(string? From, string? To);

/// <param name="Pageimage">The article's main image file name, without "File:".</param>
internal sealed record MediaWikiPage(
    string? Title,
    bool? Missing,
    string? Pageimage,
    List<MediaWikiImageInfo?>? Imageinfo);

/// <param name="Thumburl">A rendition at the requested width, or the original when it is smaller.</param>
/// <param name="Descriptionurl">The file's page on Commons.</param>
/// <param name="Extmetadata">Licence and author fields: License, AttributionRequired, Artist, Attribution, Restrictions, NonFree, ...</param>
internal sealed record MediaWikiImageInfo(
    string? Url,
    int? Width,
    int? Height,
    string? Thumburl,
    int? Thumbwidth,
    int? Thumbheight,
    string? Descriptionurl,
    string? Mime,
    Dictionary<string, MediaWikiMetadataValue?>? Extmetadata);

/// <param name="Value">Usually a string, sometimes a number.</param>
internal sealed record MediaWikiMetadataValue(JsonElement Value);
