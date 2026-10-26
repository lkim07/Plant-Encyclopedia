using System.Text;

namespace PlantEncyclopedia.Application.Retail;

public enum RetailerImportStatus
{
    Imported,

    /// <summary>The source could not be read. Nothing changed.</summary>
    Unavailable,

    /// <summary>The snapshot is much smaller than what is stored (likely a partial answer). Nothing changed.</summary>
    Suspicious
}

public sealed record RetailerImportResult(RetailerImportStatus Status, int Received, int Accepted, RetailerImportSummary? Changes = null);

/// <summary>
/// Refreshes the stored shops from the outside source (the <c>import-retailers</c> command). Records are untrusted:
/// names, addresses and websites are normalized and length-checked, websites must be absolute http(s) addresses, and
/// coordinates must be valid. A snapshot with fewer than half the stored shops is refused, so a partial answer never
/// wipes the data.
/// </summary>
public sealed class RetailerImportService(IRetailerSource source, IRetailerStore store)
{
    public async Task<RetailerImportResult> ImportAsync(CancellationToken cancellationToken)
    {
        var snapshot = await source.FetchAsync(cancellationToken);
        if (snapshot is null)
        {
            return new RetailerImportResult(RetailerImportStatus.Unavailable, 0, 0);
        }

        var accepted = snapshot.Records
            .Select(Clean)
            .OfType<RetailerRecord>()
            .DistinctBy(r => r.SourceId)
            .ToList();

        var stored = await store.CountAsync(snapshot.Source, cancellationToken);
        if (accepted.Count == 0 || accepted.Count < stored / 2)
        {
            return new RetailerImportResult(RetailerImportStatus.Suspicious, snapshot.Records.Count, accepted.Count);
        }

        var changes = await store.ReplaceAsync(snapshot with { Records = accepted }, cancellationToken);
        return new RetailerImportResult(RetailerImportStatus.Imported, snapshot.Records.Count, accepted.Count, changes);
    }

    /// <summary>The record with tidy text, or null when it cannot be shown (no name, bad coordinates).</summary>
    public static RetailerRecord? Clean(RetailerRecord record)
    {
        var name = Text(record.Name, 200);
        var sourceId = Text(record.SourceId, 64);
        if (name is null || sourceId is null
            || !double.IsFinite(record.Latitude) || record.Latitude is < -90 or > 90
            || !double.IsFinite(record.Longitude) || record.Longitude is < -180 or > 180)
        {
            return null;
        }

        return record with
        {
            SourceId = sourceId,
            Name = name,
            Street = Text(record.Street, 200),
            City = Text(record.City, 100),
            Region = Text(record.Region, 100),
            Postcode = Text(record.Postcode, 20),
            Website = Website(record.Website)
        };
    }

    // NFC, whitespace runs collapsed, no control characters; too long counts as missing.
    private static string? Text(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = string.Join(' ', value.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= maxLength && !text.Any(char.IsControl) ? text : null;
    }

    // Shown as a link, so only absolute http(s) addresses without credentials are kept.
    private static string? Website(string? value)
    {
        var text = Text(value, 500);
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && string.IsNullOrEmpty(uri.UserInfo)
                ? uri.AbsoluteUri
                : null;
    }
}
