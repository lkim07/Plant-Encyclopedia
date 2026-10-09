namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// Whether a plant's care information has been reviewed.
/// Having a source record does not make care information verified.
/// </summary>
public enum CareVerificationStatus
{
    Unverified,
    Verified
}
