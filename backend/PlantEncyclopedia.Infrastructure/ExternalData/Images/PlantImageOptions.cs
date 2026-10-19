namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>
/// Configuration section ExternalPlantData:Images. Image import is off unless Enabled is true, and the API always
/// turns it off in the Testing environment. Stored files are served from <see cref="RequestPath"/>.
/// </summary>
public sealed class PlantImageOptions
{
    public const string SectionName = "ExternalPlantData:Images";

    /// <summary>The URL prefix stored image files are served under.</summary>
    public const string RequestPath = "/media";

    public bool Enabled { get; set; }

    /// <summary>
    /// The folder stored images are written to and served from. Relative paths are resolved against the API's
    /// content root by the API before the options reach Infrastructure.
    /// </summary>
    public string StoragePath { get; set; } = "App_Data/plant-images";

    /// <summary>At startup, queue species-level plants that have no image at all (at most <see cref="BackfillLimit"/>).</summary>
    public bool BackfillOnStartup { get; set; }

    public int BackfillLimit { get; set; } = 50;

    /// <summary>Throws when image import is enabled with unsafe settings.</summary>
    public void Validate()
    {
        if (!Enabled) return;

        if (!Path.IsPathFullyQualified(StoragePath))
        {
            throw new InvalidOperationException($"{SectionName}:StoragePath must be resolved to an absolute path.");
        }

        if (BackfillLimit is < 0 or > 1_000)
        {
            throw new InvalidOperationException($"{SectionName}:BackfillLimit must be between 0 and 1000.");
        }
    }
}
