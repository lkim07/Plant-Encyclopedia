using System.Text.RegularExpressions;

namespace PlantEncyclopedia.Infrastructure.Identification;

/// <summary>
/// Configuration section Identification:PlantNet. Identification is off unless Enabled is true, and the API always
/// turns it off in the Testing environment. The API key belongs in User Secrets (or an environment variable), never
/// in a committed file.
/// </summary>
public sealed partial class PlantNetOptions
{
    public const string SectionName = "Identification:PlantNet";

    public bool Enabled { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>The Pl@ntNet project (flora) to identify against; "all" covers every flora.</summary>
    public string Project { get; set; } = "all";

    /// <summary>Throws when identification is enabled with incomplete or unsafe settings.</summary>
    public void Validate()
    {
        if (!Enabled) return;

        if (string.IsNullOrWhiteSpace(ApiKey) || ApiKey.Any(char.IsWhiteSpace) || ApiKey.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{SectionName}:ApiKey is required when identification is enabled. Set it with User Secrets.");
        }

        if (!ProjectPattern().IsMatch(Project))
        {
            throw new InvalidOperationException($"{SectionName}:Project must be a Pl@ntNet project id such as \"all\".");
        }
    }

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex ProjectPattern();
}
