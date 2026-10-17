using System.Text.Json;

namespace PlantEncyclopedia.Infrastructure.ExternalData.ChecklistBank;

// One item of GET /dataset/{key}/taxon/{id}/vernacular (a JSON array). Recorded examples:
// PlantEncyclopedia.Tests/Fixtures/ChecklistBank. This type never leaves the adapter.

/// <param name="Language">ISO 639-3 code, e.g. "eng"; missing for some names.</param>
/// <param name="ReferenceId">The cited source, when there is one (a string ID in the recordings).</param>
internal sealed record ChecklistBankVernacularName(string? Name, string? Language, JsonElement? ReferenceId);
