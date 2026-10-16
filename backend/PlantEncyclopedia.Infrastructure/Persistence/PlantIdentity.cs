namespace PlantEncyclopedia.Infrastructure.Persistence;

/// <summary>
/// Database-enforced plant identity (migration AddPlantIdentityKey, DATABASE_DESIGN §59):
/// one plant per (normalized scientific name, normalized cultivar name), whatever its status.
/// </summary>
internal static class PlantIdentity
{
    /// <summary>The unique index; a duplicate insert fails with unique_violation (23505) naming it.</summary>
    public const string UniqueIndexName = "ux_plants_identity";
}
