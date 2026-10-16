namespace PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

/// <summary>
/// Configuration section ExternalPlantData:Gbif. The adapter is off unless Enabled is true, and the API
/// always turns it off in the Testing environment.
/// </summary>
public sealed class GbifOptions
{
    public const string SectionName = "ExternalPlantData:Gbif";

    public bool Enabled { get; set; }

    /// <summary>
    /// The checklist to match against. Catalogue of Life eXtended Release (COL XR) is
    /// 7ddf754f-d193-4cc9-b351-99906754a03b. Without it GBIF falls back to the frozen 2023 Backbone,
    /// so it is required.
    /// </summary>
    public string? ChecklistKey { get; set; }

    /// <summary>A public URL identifying this application, sent in the User-Agent so GBIF can reach the operator.</summary>
    public string? Contact { get; set; }

    /// <summary>Throws when the adapter is enabled with incomplete or unsafe settings.</summary>
    public void Validate()
    {
        if (!Enabled) return;

        if (!Guid.TryParseExact(ChecklistKey, "D", out _))
        {
            throw new InvalidOperationException($"{SectionName}:ChecklistKey must be a GBIF dataset key (GUID).");
        }

        if (!Uri.TryCreate(Contact, UriKind.Absolute, out var contact)
            || contact.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(contact.UserInfo))
        {
            throw new InvalidOperationException($"{SectionName}:Contact must be an absolute https URL without credentials.");
        }
    }
}
