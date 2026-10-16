namespace PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

// The parts of GET /v2/species/match that the adapter reads. Recorded examples:
// PlantEncyclopedia.Tests/Fixtures/Gbif. These types never leave the adapter.

internal sealed record GbifMatchResponse(
    GbifUsage? Usage,
    List<GbifClassificationEntry>? Classification,
    GbifDiagnostics? Diagnostics,
    bool? Synonym);

/// <param name="Name">Name with authorship and markers, e.g. "Mentha × piperita L.".</param>
/// <param name="CanonicalName">Name without hybrid sign or rank marker, e.g. "Mentha piperita"; not used.</param>
internal sealed record GbifUsage(
    string? Key,
    string? Name,
    string? CanonicalName,
    string? Authorship,
    string? Rank,
    string? Status);

internal sealed record GbifClassificationEntry(string? Key, string? Name, string? Rank);

/// <param name="MatchType">EXACT, VARIANT (fuzzy), HIGHERRANK or NONE.</param>
internal sealed record GbifDiagnostics(string? MatchType);
