using PlantEncyclopedia.Domain.Common;

namespace PlantEncyclopedia.Domain.Plants;

/// <summary>
/// Quick Care information: light, water, temperature and soil.
/// Every field is optional because care data may be missing or incomplete.
/// Provenance is recorded by PlantSource rows with SourceType = Care;
/// verification is tracked separately here and covers the whole record.
/// </summary>
public class PlantCare : IHasTimestamps
{
    public Guid PlantId { get; set; }

    public string? LightSummary { get; set; }
    public string? LightDetails { get; set; }

    public string? WaterSummary { get; set; }
    public string? WaterDetails { get; set; }

    public decimal? TemperatureMinC { get; set; }
    public decimal? TemperatureMaxC { get; set; }
    public string? TemperatureDetails { get; set; }

    public string? SoilSummary { get; set; }
    public string? SoilDetails { get; set; }

    public CareVerificationStatus VerificationStatus { get; set; } = CareVerificationStatus.Unverified;
    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Plant? Plant { get; set; }
}
