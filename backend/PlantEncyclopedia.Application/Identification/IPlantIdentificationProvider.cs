using PlantEncyclopedia.Application.Explore;

namespace PlantEncyclopedia.Application.Identification;

/// <summary>A species the provider suggests for a photo; the score is internal and never shown (D-013).</summary>
public sealed record ProviderCandidate(string ScientificName, double Score);

public enum ProviderIdentificationStatus
{
    /// <summary>The provider answered; its list may be empty.</summary>
    Answered,

    /// <summary>Disabled, over budget, timed out or failed. Never cached and never shown as "no such plant".</summary>
    Unavailable
}

public sealed record ProviderIdentification(ProviderIdentificationStatus Status, IReadOnlyList<ProviderCandidate> Candidates)
{
    public static readonly ProviderIdentification Unavailable = new(ProviderIdentificationStatus.Unavailable, []);

    public static ProviderIdentification Answered(IReadOnlyList<ProviderCandidate> candidates) =>
        new(ProviderIdentificationStatus.Answered, candidates);
}

/// <summary>
/// An image identification service (ARCHITECTURE §26), replaceable behind this interface. It only suggests
/// names; nothing it returns becomes encyclopedia data (D-007, D-011).
/// </summary>
public interface IPlantIdentificationProvider
{
    /// <param name="jpeg">A photo already checked and re-encoded by <see cref="IIdentificationImageProcessor"/>.</param>
    Task<ProviderIdentification> IdentifyAsync(byte[] jpeg, CancellationToken cancellationToken);
}

/// <summary>Checks an uploaded photo and prepares the copy sent to the provider (SECURITY §10–11).</summary>
public interface IIdentificationImageProcessor
{
    /// <summary>
    /// A JPEG of the photo, upright and at most a set size, with no metadata (location included); or null when the
    /// bytes are not an acceptable JPEG, PNG or WebP image.
    /// </summary>
    byte[]? Prepare(byte[] upload);
}

/// <summary>A Published plant matching a provider's name.</summary>
/// <param name="CandidateIndex">The position of the matching name in the list that was looked up.</param>
/// <param name="PlantGroupSlug">The plant group's slug (its ID in plant-group URLs), or null without a group.</param>
public sealed record MatchedPlant(
    int CandidateIndex, Guid Id, string Name, string? PlantGroupName, string? PlantGroupSlug, string ScientificName, string? ImageUrl);

/// <summary>Finds the encyclopedia's plants for suggested names (implemented in Infrastructure).</summary>
public interface IIdentificationQueries
{
    /// <summary>
    /// Published plants whose scientific name has the same identity as one of <paramref name="scientificNames"/>,
    /// in the order of the names; for each name, the species itself before its cultivars.
    /// </summary>
    Task<IReadOnlyList<MatchedPlant>> FindPublishedAsync(IReadOnlyList<string> scientificNames, CancellationToken cancellationToken);

    /// <summary>The plant, when it is Published (used after an import); its CandidateIndex is 0.</summary>
    Task<MatchedPlant?> FindPublishedByIdAsync(Guid plantId, CancellationToken cancellationToken);

    /// <summary>
    /// The More page (PRD §30, UX §37): Published plants related to the candidates, closest first: the same plant group,
    /// then the same genus, the same family, the same category; the earlier candidate first within each; then plants
    /// with an image, then by name. Each plant once; the candidates themselves are left out.
    /// </summary>
    Task<ExplorePage> GetRelatedAsync(IReadOnlyList<Guid> candidateIds, int page, int pageSize, CancellationToken cancellationToken);
}
