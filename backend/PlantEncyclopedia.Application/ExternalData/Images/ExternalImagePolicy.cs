using System.Text.RegularExpressions;

namespace PlantEncyclopedia.Application.ExternalData.Images;

public enum ImageRejection
{
    /// <summary>No licence, or one that forbids commercial use or changes (NC, ND), or one that is not recognized.</summary>
    LicenseNotAllowed,

    /// <summary>The licence or provider requires credit, but no author is given.</summary>
    MissingCreator,

    /// <summary>Not a JPEG, PNG or WebP file.</summary>
    UnsupportedType,

    /// <summary>The longer side is under <see cref="ExternalImagePolicy.MinLongSide"/> pixels.</summary>
    TooSmall,

    InvalidData
}

/// <summary>An image the policy accepted, with its licence in canonical form.</summary>
/// <param name="LicenseCode">Canonical code: "pd", "cc0", "cc-by", "cc-by-4.0", "cc-by-sa", "cc-by-sa-3.0", ...</param>
/// <param name="LicenseName">For credit lines, e.g. "CC BY-SA 3.0" or "Public domain".</param>
/// <param name="LicenseUrl">The licence deed, only when the version is known.</param>
public sealed record AcceptedImage(
    ExternalImageCandidate Image,
    string LicenseCode,
    string LicenseName,
    Uri? LicenseUrl,
    string? Creator);

/// <summary>
/// Decides whether an external image may be used (ARCHITECTURE §22). Only licences that allow commercial use
/// and changes such as resizing are accepted: public domain, CC0, CC BY and CC BY-SA, any version.
/// NC and ND licences, "all rights reserved", unknown codes and missing licences are rejected. When credit
/// is required (CC BY, CC BY-SA, or the provider says so), the author must be known.
/// </summary>
public static partial class ExternalImagePolicy
{
    /// <summary>Large enough for the detail-page hero.</summary>
    public const int MinLongSide = 1000;

    private const int MaxSide = 20_000;
    private const int CreatorMaxLength = 300;
    private const int SourceIdMaxLength = 500;

    private static readonly HashSet<string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public static ImageRejection? Evaluate(ExternalImageCandidate image, out AcceptedImage? accepted)
    {
        accepted = null;

        var license = ParseLicense(image.LicenseCode);
        if (license is null) return ImageRejection.LicenseNotAllowed;

        var creator = ExternalTaxonPolicy.NormalizeText(image.Creator);
        if (creator is not null && !ExternalTaxonPolicy.IsValidText(creator, CreatorMaxLength)) return ImageRejection.InvalidData;
        if ((license.Value.RequiresAttribution || image.ProviderRequiresAttribution) && creator is null) return ImageRejection.MissingCreator;

        if (image.MimeType is null || !MimeTypes.Contains(image.MimeType)) return ImageRejection.UnsupportedType;
        if (image.Width <= 0 || image.Height <= 0 || image.Width > MaxSide || image.Height > MaxSide) return ImageRejection.InvalidData;
        if (Math.Max(image.Width, image.Height) < MinLongSide) return ImageRejection.TooSmall;

        if (image.DownloadUrl.Scheme != Uri.UriSchemeHttps
            || image.SourcePageUrl is { Scheme: not "https" }
            || !ExternalTaxonPolicy.IsValidText(image.SourceId, SourceIdMaxLength))
        {
            return ImageRejection.InvalidData;
        }

        accepted = new AcceptedImage(image, license.Value.Code, license.Value.Name, license.Value.Url, creator);
        return null;
    }

    /// <summary>
    /// The display name and deed of a stored licence code, e.g. "cc-by-sa-3.0" → ("CC BY-SA 3.0", its deed).
    /// Null for codes this policy would not accept.
    /// </summary>
    public static (string Name, Uri? Url)? Describe(string? licenseCode) =>
        ParseLicense(licenseCode) is { } license ? (license.Name, license.Url) : null;

    private static (string Code, string Name, Uri? Url, bool RequiresAttribution)? ParseLicense(string? code)
    {
        if (code is null) return null;

        var match = LicensePattern().Match(code.Trim().ToLowerInvariant());
        if (!match.Success) return null;

        var kind = match.Groups["kind"].Value;
        var version = match.Groups["version"].Success ? match.Groups["version"].Value : null;
        var canonical = version is null ? kind : $"{kind}-{version}";

        return kind switch
        {
            "pd" => (canonical, "Public domain", null, false),
            "cc0" => (canonical, "CC0", new Uri("https://creativecommons.org/publicdomain/zero/1.0/"), false),
            _ => (canonical,
                (kind == "cc-by" ? "CC BY" : "CC BY-SA") + (version is null ? "" : " " + version),
                version is null ? null : new Uri($"https://creativecommons.org/licenses/{kind[3..]}/{version}/"),
                true)
        };
    }

    // "pd", "cc0", "cc0-1.0", "cc-by", "cc-by-4.0", "cc-by-sa", "cc-by-sa-3.0", "cc-by-sa-2.5-nl" is not accepted
    // (ported licences need their own deed), nor anything with "nc" or "nd".
    [GeneratedRegex(@"^(?:(?<kind>pd)|(?<kind>cc0)(?:-1\.0)?|(?<kind>cc-by(?:-sa)?)(?:-(?<version>[1-4]\.0|2\.5))?)$")]
    private static partial Regex LicensePattern();
}
